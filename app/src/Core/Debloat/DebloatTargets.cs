using System.Text.RegularExpressions;

namespace PCManager.Core.Debloat;

public enum DebloatTargetKind
{
    CurrentUser,
    OtherUser,
    DefaultUser,
}

/// <summary>One local profile from ProfileList, without secrets or file contents.</summary>
public sealed record DebloatUserProfile(
    string Name,
    string Sid,
    string ProfilePath,
    bool IsLoaded)
{
    public bool IsCurrentUser { get; init; }
}

/// <summary>
/// A resolved, typed target. MountRoot is either HKCU, a live HKU\&lt;sid&gt; hive, or our own
/// HKU\FPM_* mount that the helper must load and unload in a finally block.
/// </summary>
public sealed record DebloatTarget(
    string Token,
    DebloatTargetKind Kind,
    string DisplayName,
    string MountRoot,
    bool NeedsLoad,
    string? HiveFile,
    string Warning);

public sealed class DebloatTargetResolution
{
    public bool Success => Target is not null;
    public DebloatTarget? Target { get; init; }
    public string Message { get; init; } = string.Empty;

    public static DebloatTargetResolution Ok(DebloatTarget target) => new() { Target = target };

    public static DebloatTargetResolution Failed(string message) => new() { Message = message };
}

/// <summary>
/// Resolves the user token sent over the helper pipe. The client can never send a hive path;
/// only "current", "default", or a profile name/SID that exists in ProfileList.
/// </summary>
public static class DebloatTargetResolver
{
    public const string DefaultToken = "default";
    private static readonly Regex SidPattern = new("^S-1-[0-9-]+$", RegexOptions.CultureInvariant);

    public static DebloatTargetResolution Resolve(
        string? token,
        IReadOnlyList<DebloatUserProfile> profiles,
        string currentSid,
        int osBuild,
        string? defaultHiveFile = null)
    {
        string value = (token ?? string.Empty).Trim();

        if (value.Length == 0 ||
            value.Equals("current", StringComparison.OrdinalIgnoreCase) ||
            value.Equals(currentSid, StringComparison.OrdinalIgnoreCase))
        {
            return DebloatTargetResolution.Ok(new DebloatTarget(
                "current", DebloatTargetKind.CurrentUser, "This account", "HKCU", false, null,
                "Changes apply to your signed-in account."));
        }

        if (value.Equals(DefaultToken, StringComparison.OrdinalIgnoreCase))
        {
            if (osBuild < 10240)
            {
                return DebloatTargetResolution.Failed(
                    "The Default user hive is only available on Windows 10 or newer.");
            }

            string hive = defaultHiveFile ?? System.IO.Path.Combine(
                Environment.GetEnvironmentVariable("SystemDrive") ?? "C:", "Users", "Default", "NTUSER.DAT");

            return DebloatTargetResolution.Ok(new DebloatTarget(
                DefaultToken, DebloatTargetKind.DefaultUser,
                "Default user (sysprep)", @"HKU\FPM_Default", true, hive,
                "SYSPREP TARGET: this changes the profile Windows copies to every new account. " +
                "Use only while preparing an image; the helper unloads the hive when it finishes."));
        }

        string lookup = value.StartsWith("user:", StringComparison.OrdinalIgnoreCase)
            ? value[5..].Trim()
            : value.StartsWith("sid:", StringComparison.OrdinalIgnoreCase)
                ? value[4..].Trim()
                : value;

        if (lookup.Length == 0 || lookup.Length > 120)
        {
            return DebloatTargetResolution.Failed("Choose a local profile name or SID.");
        }

        DebloatUserProfile? profile = profiles.FirstOrDefault(candidate =>
            candidate.Sid.Equals(lookup, StringComparison.OrdinalIgnoreCase) ||
            candidate.Name.Equals(lookup, StringComparison.OrdinalIgnoreCase));

        if (profile is null)
        {
            return DebloatTargetResolution.Failed(
                $"No local profile matches '{lookup}'. Refresh the profile list and try again.");
        }

        if (profile.Sid.Equals(currentSid, StringComparison.OrdinalIgnoreCase) || profile.IsCurrentUser)
        {
            return DebloatTargetResolution.Ok(new DebloatTarget(
                "current", DebloatTargetKind.CurrentUser, "This account", "HKCU", false, null,
                "Changes apply to your signed-in account."));
        }

        if (!SidPattern.IsMatch(profile.Sid))
        {
            return DebloatTargetResolution.Failed("That profile has an invalid SID.");
        }

        if (profile.IsLoaded)
        {
            string liveMount = @"HKU\" + profile.Sid;

            return DebloatTargetResolution.Ok(new DebloatTarget(
                "sid:" + profile.Sid, DebloatTargetKind.OtherUser,
                $"{profile.Name} (signed in, live hive)", liveMount, false, null,
                "This account is signed in. FPM uses its live hive; changes take effect immediately."));
        }

        string mount = @"HKU\FPM_" + profile.Sid.Replace("-", string.Empty, StringComparison.Ordinal);

        return DebloatTargetResolution.Ok(new DebloatTarget(
            "sid:" + profile.Sid, DebloatTargetKind.OtherUser,
            profile.Name, mount, true,
            System.IO.Path.Combine(profile.ProfilePath, "NTUSER.DAT"),
            $"Offline profile: the helper loads {profile.Name}'s NTUSER.DAT, applies the tweaks, " +
            "and unloads it in a finally block."));
    }
}

/// <summary>Thin seam over RegLoadKey/RegUnloadKey so the unload path is testable.</summary>
public interface IDebloatHiveApi
{
    bool IsLoaded(string mountKey);
    void Load(string mountKey, string hiveFile);
    void Unload(string mountKey);
}

/// <summary>
/// Loads a target hive only when the target needs it, and always unloads it — including when
/// an apply throws. The helper keeps this object in a using block for the whole command.
/// </summary>
public sealed class DebloatHiveScope : IDisposable
{
    private readonly IDebloatHiveApi? _api;
    private readonly string _mountKey;
    private bool _loaded;
    private bool _disposed;

    private DebloatHiveScope(IDebloatHiveApi api, string mountKey)
    {
        _api = api;
        _mountKey = mountKey;
    }

    public string? UnloadError { get; private set; }

    public static DebloatHiveScope? Acquire(IDebloatHiveApi api, DebloatTarget target)
    {
        if (!target.NeedsLoad)
        {
            return null;
        }

        string mountKey = target.MountRoot.StartsWith(@"HKU\", StringComparison.OrdinalIgnoreCase)
            ? target.MountRoot[4..]
            : throw new InvalidOperationException("An offline target must mount under HKU.");

        if (api.IsLoaded(mountKey))
        {
            throw new InvalidOperationException(
                $"The hive {target.MountRoot} is already loaded. Unload it before trying again.");
        }

        if (string.IsNullOrWhiteSpace(target.HiveFile) || !File.Exists(target.HiveFile))
        {
            throw new FileNotFoundException(
                $"The profile hive was not found: {target.HiveFile}", target.HiveFile);
        }

        var scope = new DebloatHiveScope(api, mountKey);
        api.Load(mountKey, target.HiveFile!);
        scope._loaded = true;
        return scope;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (!_loaded || _api is null)
        {
            return;
        }

        try
        {
            _api.Unload(_mountKey);
        }
        catch (Exception ex)
        {
            // Never hide the original apply failure; the helper reports this after the scope.
            UnloadError = ex.Message;
        }
    }
}
