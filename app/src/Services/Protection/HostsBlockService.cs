using System.Text.RegularExpressions;
using PCManager.Core.Elevation;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Protection;

/// <summary>
/// Reads the FPM section of the Windows hosts file and asks the elevated helper to replace or
/// remove it. The helper writes a timestamped backup before touching the file.
/// </summary>
public sealed class HostsBlockService : IHostsBlockService
{
    internal const string BeginMarker = "# BEGIN FPM";
    internal const string EndMarker = "# END FPM";
    internal const int MaxDomains = 100;

    private static readonly Regex DomainPattern = new(
        @"^(?=.{1,253}$)([a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IElevationService _elevation;
    private readonly IAppLogger _logger;

    public HostsBlockService(IElevationService elevation, IAppLogger logger)
    {
        _elevation = elevation;
        _logger = logger;
    }

    public HostsBlockState GetState()
    {
        try
        {
            string hostsPath = GetHostsPath();

            if (!File.Exists(hostsPath))
            {
                return HostsBlockState.None("The Windows hosts file was not found.");
            }

            string[] lines = File.ReadAllLines(hostsPath);
            int count = CountBlockedDomains(lines);
            return count == 0
                ? HostsBlockState.None("No FPM hosts block is active.")
                : new HostsBlockState(count, $"FPM is blocking {count} domain(s) in the hosts file.");
        }
        catch (Exception ex)
        {
            _logger.Error("Could not read the hosts file.", ex);
            return HostsBlockState.None($"Could not read the hosts file: {ex.Message}");
        }
    }

    public async Task<OperationResult> ApplyAsync(string domainsText, CancellationToken cancellationToken = default)
    {
        if (!TryParseDomains(domainsText, out IReadOnlyList<string> domains, out string error))
        {
            return OperationResult.Fail(error);
        }

        if (!_elevation.IsHelperAvailable)
        {
            return OperationResult.Fail("The elevated helper is missing from the application folder.");
        }

        ElevationOutcome outcome = await _elevation
            .RunAsync(HelperCommand.ManageHostsBlock, domains: domains, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new OperationResult(outcome.Success, outcome.Message);
    }

    public async Task<OperationResult> RemoveAsync(CancellationToken cancellationToken = default)
    {
        ElevationOutcome outcome = await _elevation
            .RunAsync(HelperCommand.ManageHostsBlock, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new OperationResult(outcome.Success, outcome.Message);
    }

    /// <summary>Comma, semicolon, space or newline separated DNS names; duplicates removed.</summary>
    public static bool TryParseDomains(string text, out IReadOnlyList<string> domains, out string error)
    {
        domains = Array.Empty<string>();
        error = string.Empty;

        string[] candidates = (text ?? string.Empty)
            .Split(new[] { ',', ';', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.Trim('.').ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidates.Length == 0)
        {
            error = "Enter at least one domain to block.";
            return false;
        }

        if (candidates.Length > MaxDomains)
        {
            error = $"Block at most {MaxDomains} domains at a time.";
            return false;
        }

        string? invalid = candidates.FirstOrDefault(domain => !DomainPattern.IsMatch(domain));

        if (invalid is not null)
        {
            error = $"'{invalid}' is not a valid domain name.";
            return false;
        }

        domains = candidates;
        return true;
    }

    internal static string GetHostsPath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "drivers",
            "etc",
            "hosts");

    public static int CountBlockedDomains(IEnumerable<string> lines)
    {
        bool inside = false;
        int count = 0;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (trimmed.Equals(BeginMarker, StringComparison.OrdinalIgnoreCase))
            {
                inside = true;
                continue;
            }

            if (trimmed.Equals(EndMarker, StringComparison.OrdinalIgnoreCase))
            {
                inside = false;
                continue;
            }

            if (inside && trimmed.Length > 0 && !trimmed.StartsWith('#'))
            {
                count++;
            }
        }

        return count;
    }
}
