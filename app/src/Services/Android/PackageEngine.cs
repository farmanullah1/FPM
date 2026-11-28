using PCManager.Core.Android;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Android;

/// <summary>
/// Read-only package state from the typed ADB surface, then a sequential batch. A failed package
/// does not abort the batch; a disconnected device does. Every successful batch re-scans to verify
/// the state instead of trusting the command exit code.
/// </summary>
public sealed class PackageEngine : IPackageEngine
{
    private readonly IPackageListProvider _lists;
    private readonly IAdbCommandExecutor _adb;
    private readonly IAppLogger _logger;

    public PackageEngine(
        IPackageListProvider lists,
        IAdbCommandExecutor adb,
        IAppLogger logger)
    {
        _lists = lists;
        _adb = adb;
        _logger = logger;
    }

    public async Task<PackageScanResult> ScanAsync(
        AdbDevice device,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (!device.IsUsable)
        {
            return new PackageScanResult(
                PackageScanStatus.Unavailable,
                "The Android device is not authorized; package scanning is read-only and needs a ready device.",
                null);
        }

        if (userId < 0)
        {
            return new PackageScanResult(
                PackageScanStatus.Failed,
                "Android user id must be zero or greater.",
                null);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new PackageScanResult(PackageScanStatus.Failed, "Package scan was cancelled.", null);
        }

        UadListCatalog catalog;

        try
        {
            catalog = _lists.Current ?? await _lists.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new PackageScanResult(
                PackageScanStatus.Failed,
                "The UAD package list is unavailable: " + ex.Message,
                null);
        }

        var outputs = new Dictionary<PackageListFilter, string>();

        foreach (PackageListFilter filter in new[]
                 {
                     PackageListFilter.All,
                     PackageListFilter.System,
                     PackageListFilter.Enabled,
                     PackageListFilter.Disabled,
                 })
        {
            AdbProcessResult result = await _adb
                .ExecuteAsync(
                    device,
                    AdbCommand.ListPackages(device.Serial, userId, filter),
                    cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                return PackageEngineRules.Failure(result, "Package scan failed");
            }

            outputs[filter] = result.StandardOutput;
        }

        HashSet<string> all = PackageEngineRules.ToSet(outputs[PackageListFilter.All]);
        HashSet<string> system = PackageEngineRules.ToSet(outputs[PackageListFilter.System]);
        HashSet<string> enabled = PackageEngineRules.ToSet(outputs[PackageListFilter.Enabled]);
        HashSet<string> disabled = PackageEngineRules.ToSet(outputs[PackageListFilter.Disabled]);

        var packages = all
            .Select(id => new AndroidPackage(
                id,
                enabled.Contains(id)
                    ? PackageState.Enabled
                    : disabled.Contains(id)
                        ? PackageState.Disabled
                        : PackageState.Uninstalled,
                system.Contains(id),
                catalog.ById.GetValueOrDefault(id)))
            .OrderBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var scan = new PackageScan(device.Serial, userId, packages);
        _logger.Info(
            $"Android package scan: {scan.Packages.Count} package(s), " +
            $"{scan.SystemCount} system, user {userId}.");

        return new PackageScanResult(
            PackageScanStatus.Ready,
            $"{scan.Packages.Count} package(s) scanned.",
            scan);
    }

    public async Task<PackageBatchResult> ApplyAsync(
        AdbDevice device,
        int userId,
        IReadOnlyCollection<PackageActionRequest> requests,
        PackageApplyOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new PackageApplyOptions();
        PackageActionRequest[] actions = requests.ToArray();
        PackageScanResult scanResult = await ScanAsync(device, userId, cancellationToken)
            .ConfigureAwait(false);

        if (scanResult.Status != PackageScanStatus.Ready || scanResult.Scan is null)
        {
            return new PackageBatchResult(
                scanResult.Status == PackageScanStatus.Disconnected
                    ? PackageBatchStatus.StoppedDisconnected
                    : PackageBatchStatus.NotStarted,
                scanResult.Message,
                Array.Empty<PackageActionResult>(),
                actions.Length,
                false,
                null,
                Array.Empty<PackageDependencyWarning>());
        }

        PackageScan scan = scanResult.Scan;
        var results = new List<PackageActionResult>();
        PackageBatchStatus status = PackageBatchStatus.Completed;
        int remaining = 0;
        string message = string.Empty;

        foreach (PackageActionRequest request in actions)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                status = PackageBatchStatus.Cancelled;
                remaining = actions.Length - results.Count;
                message = "Package batch cancelled.";
                break;
            }

            if (!scan.ById.TryGetValue(request.PackageId, out AndroidPackage? package))
            {
                results.Add(PackageEngineRules.Result(request, request.Action, PackageState.Uninstalled, null,
                    PackageResultStatus.Failed,
                    "The package was not present in the scan."));
                continue;
            }

            if (!AdbCommandLine.IsValidPackageId(request.PackageId))
            {
                results.Add(PackageEngineRules.Result(request, request.Action, package.State, null,
                    PackageResultStatus.Failed,
                    "The package id is malformed."));
                continue;
            }

            if (package.IsDenylisted() &&
                !(options.ExpertMode &&
                  string.Equals(options.ExpertConfirmation, package.Id, StringComparison.Ordinal)))
            {
                results.Add(PackageEngineRules.Result(request, request.Action, package.State, null,
                    PackageResultStatus.BlockedDenylist,
                    PackageDenylist.Message));
                continue;
            }

            PackageAction action = options.DisableInsteadOfUninstall &&
                                   request.Action == PackageAction.Uninstall
                ? PackageAction.Disable
                : request.Action;

            if (PackageEngineRules.AlreadyInState(package, action, out string alreadyMessage))
            {
                results.Add(PackageEngineRules.Result(request, action, package.State, package.State,
                    PackageResultStatus.Already, alreadyMessage));
                continue;
            }

            if (!PackageEngineRules.CanApply(package, action, out string invalidMessage))
            {
                results.Add(PackageEngineRules.Result(request, action, package.State, null,
                    PackageResultStatus.Failed, invalidMessage));
                continue;
            }

            AdbCommand command;

            try
            {
                command = PackageEngineRules.CommandFor(device, package.Id, action, userId);
            }
            catch (ArgumentException ex)
            {
                results.Add(PackageEngineRules.Result(request, action, package.State, null,
                    PackageResultStatus.Failed, ex.Message));
                continue;
            }

            AdbProcessResult commandResult = await _adb
                .ExecuteAsync(device, command, cancellationToken)
                .ConfigureAwait(false);

            if (commandResult.Success)
            {
                results.Add(PackageEngineRules.Result(request, action, package.State, null,
                    PackageResultStatus.Ok,
                    $"{action} command completed."));
                continue;
            }

            string failure = PackageEngineRules.Message(commandResult);
            results.Add(PackageEngineRules.Result(request, action, package.State, null,
                PackageResultStatus.Failed, failure));

            if (AdbFailures.IsDeviceDisconnect(commandResult))
            {
                status = PackageBatchStatus.StoppedDisconnected;
                remaining = actions.Length - results.Count;
                message = "The device disconnected; the remaining package actions were not run.";
                _logger.Error($"Android package batch stopped: {failure}");
                break;
            }
        }

        bool changed = results.Any(result => result.Status == PackageResultStatus.Ok);
        bool verified = false;
        PackageScan? verifiedScan = null;
        string? verificationMessage = null;

        if (status == PackageBatchStatus.Completed && changed)
        {
            PackageScanResult verify = await ScanAsync(device, userId, cancellationToken)
                .ConfigureAwait(false);

            if (verify.Status == PackageScanStatus.Ready && verify.Scan is not null)
            {
                verified = true;
                verifiedScan = verify.Scan;
                PackageEngineRules.ApplyVerifiedStates(results, verify.Scan);
            }
            else
            {
                verificationMessage = "State verification failed: " + verify.Message;

                if (verify.Status == PackageScanStatus.Disconnected)
                {
                    status = PackageBatchStatus.StoppedDisconnected;
                }
            }
        }

        IReadOnlyList<PackageDependencyWarning> warnings = PackageDependencyWarnings.Build(
            scan.Packages.Where(package => actions.Any(action =>
                string.Equals(action.PackageId, package.Id, StringComparison.Ordinal))));

        message = status switch
        {
            PackageBatchStatus.StoppedDisconnected => message,
            PackageBatchStatus.Cancelled => message,
            _ => PackageEngineRules.BuildSummary(results),
        };

        if (verificationMessage is not null)
        {
            message += " " + verificationMessage;
        }

        _logger.Info($"Android package batch: {message}");

        return new PackageBatchResult(
            status,
            message,
            results,
            remaining,
            verified,
            verifiedScan,
            warnings);
    }

}
