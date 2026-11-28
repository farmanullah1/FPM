using System.Diagnostics;
using System.Text.Json;
using PCManager.Core.Debloat;
using PCManager.Core.Elevation;

namespace PCManager.Helper;

/// <summary>
/// Catalog-scoped debloat commands. The client names a catalog tweak id, an optional typed user
/// target and (for revert) pre-state limited to that tweak's own targets. No path or hive file
/// ever crosses the pipe.
/// </summary>
internal static class DebloatCommands
{
    private const int MaxRestoreEntries = 128;

    private static readonly Lazy<DebloatCatalog> Catalog = new(DebloatCatalogLoader.LoadEmbedded);

    public static HelperResponse Handle(HelperRequest request)
    {
        DebloatHiveScope? scope = null;

        try
        {
            DebloatTweak? tweak = Catalog.Value.Tweaks
                .FirstOrDefault(candidate => string.Equals(candidate.Id, request.TweakId, StringComparison.Ordinal));

            if (tweak is null)
            {
                return Failure(request, $"Unknown tweak '{request.TweakId}'.");
            }

            DebloatTargetResolution resolution = HelperHive.Resolve(request.TargetUser);

            if (resolution.Target is not { } target)
            {
                return Failure(request, resolution.Message);
            }

            scope = DebloatHiveScope.Acquire(new RegistryHiveApi(), target);

            HelperResponse response;

            using (var registry = new DebloatRegistry(target))
            {
                response = request.Command switch
                {
                    HelperCommand.DetectTweak => Detect(request, tweak, registry),
                    HelperCommand.ApplyTweak => Apply(request, tweak, registry),
                    HelperCommand.RevertTweak => Revert(request, tweak, registry),
                    _ => Failure(request, "Unsupported debloat command."),
                };
            }

            scope?.Dispose();

            if (scope?.UnloadError is { Length: > 0 } unloadError)
            {
                return Failure(
                    request,
                    $"The target hive could not be unloaded: {unloadError}. " +
                    "The run's journal still records exactly what was changed.");
            }

            response.TargetUser = target.DisplayName;
            return response;
        }
        catch (Exception ex)
        {
            Log($"Debloat {request.Command} {request.TweakId} target='{request.TargetUser}' failed: {ex}");
            return Failure(request, ex.Message);
        }
        finally
        {
            scope?.Dispose();
        }
    }

    private static HelperResponse Detect(
        HelperRequest request,
        DebloatTweak tweak,
        DebloatRegistry registry)
    {
        List<RegistryValueState> values = registry.Capture(tweak.Actions);
        bool detected = tweak.Detect.All(probe => Evaluate(probe, values));

        return new HelperResponse
        {
            Success = true,
            Detected = detected,
            Values = values,
            Message = detected ? "Already applied." : "Not applied.",
            RequestId = request.RequestId,
        };
    }

    private static HelperResponse Apply(
        HelperRequest request,
        DebloatTweak tweak,
        DebloatRegistry registry)
    {
        if (tweak.Actions.Any(action => action is RegistryKeyDeleteAction or AppxRemoveAction))
        {
            return Failure(request, "This tweak's action type is not supported by the registry executor.");
        }

        List<RegistryValueState> before = registry.Capture(tweak.Actions);

        try
        {
            foreach (DebloatAction action in tweak.Actions)
            {
                if (action is OptionalFeatureAction feature)
                {
                    SetOptionalFeature(feature.FeatureName, feature.Enable);
                }
                else
                {
                    registry.Execute(action);
                }
            }

            return new HelperResponse
            {
                Success = true,
                Values = before,
                Message = $"Applied '{tweak.Title}'.",
                RequestId = request.RequestId,
            };
        }
        catch (Exception ex)
        {
            bool rolledBack = registry.TryRestore(before);
            Log($"Apply {tweak.Id} failed: {ex.Message}; rolledBack={rolledBack}");
            return new HelperResponse
            {
                Success = false,
                RolledBack = rolledBack,
                Message = $"'{tweak.Title}' failed: {ex.Message}",
                RequestId = request.RequestId,
            };
        }
    }

    private static HelperResponse Revert(
        HelperRequest request,
        DebloatTweak tweak,
        DebloatRegistry registry)
    {
        if (request.Restore.Count > MaxRestoreEntries)
        {
            return Failure(request, "The restore state is larger than the allowed limit.");
        }

        if (request.Restore.Count > 0)
        {
            string? refusal = DebloatRestoreGuard.Validate(tweak, request.Restore);
            if (refusal is not null)
            {
                return Failure(request, refusal);
            }

            return registry.TryRestore(request.Restore)
                ? new HelperResponse
                {
                    Success = true,
                    Message = $"Restored '{tweak.Title}' to its previous state.",
                    RequestId = request.RequestId,
                }
                : Failure(request, $"Could not fully restore '{tweak.Title}'.");
        }

        try
        {
            foreach (DebloatAction action in tweak.Revert)
            {
                if (action is OptionalFeatureAction feature)
                {
                    SetOptionalFeature(feature.FeatureName, feature.Enable);
                }
                else
                {
                    registry.Execute(action);
                }
            }

            return new HelperResponse
            {
                Success = true,
                Message = $"Applied the reference undo for '{tweak.Title}'.",
                RequestId = request.RequestId,
            };
        }
        catch (Exception ex)
        {
            return Failure(request, $"Revert failed: {ex.Message}");
        }
    }

    private static bool Evaluate(DebloatProbe probe, IReadOnlyList<RegistryValueState> values) => probe switch
    {
        RegistryEqualsProbe equals => values.Any(state =>
            state.Hive == equals.Hive &&
            state.Path == equals.Path &&
            state.Name == equals.RegistryName &&
            state.Existed &&
            state.ValueType == equals.ValueType &&
            SameJson(state.Value, equals.Value)),
        RegistryAbsentProbe absent => values.Any(state =>
            state.Hive == absent.Hive &&
            state.Path == absent.Path &&
            state.Name == absent.RegistryName &&
            !state.Existed),
        OptionalFeatureProbe feature => IsFeatureEnabled(feature.FeatureName) == feature.ExpectEnabled,
        _ => false,
    };

    private static bool SameJson(JsonElement? left, JsonElement right) =>
        left is { } value && value.GetRawText() == right.GetRawText();

    private static bool IsFeatureEnabled(string name)
    {
        ProcessResult result = RunDism($"/online /get-featureinfo /featurename:{name}");
        return result.Output.Contains("State : Enabled", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetOptionalFeature(string name, bool enable)
    {
        string verb = enable ? "/enable-feature" : "/disable-feature";
        ProcessResult result = RunDism($"/online {verb} /featurename:{name} /all /norestart");

        if (result.ExitCode != 0 && result.ExitCode != 3010)
        {
            throw new InvalidOperationException(
                $"DISM {verb} {name} failed with exit code {result.ExitCode}.");
        }
    }

    private static ProcessResult RunDism(string arguments)
    {
        var startInfo = new ProcessStartInfo("dism.exe", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows did not start DISM.");

        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit(120000);
        return new ProcessResult(process.ExitCode, output);
    }

    private static HelperResponse Failure(HelperRequest request, string message) => new()
    {
        Success = false,
        Message = message,
        RequestId = request.RequestId,
    };

    private static void Log(string message)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FarmanullahPCManager",
                "logs");

            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, $"helper-{DateTime.Now:yyyyMMdd}.log"),
                $"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch
        {
            // The helper must not fail because its own log is unavailable.
        }
    }

    private readonly record struct ProcessResult(int ExitCode, string Output);
}
