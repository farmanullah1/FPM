using System.IO;
using System.Security.Principal;
using Microsoft.Win32;
using PCManager.Core.Debloat;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Debloat;

public interface IDebloatProfileService
{
    IReadOnlyList<DebloatUserProfile> GetProfiles();

    DebloatTargetResolution Resolve(string? token);
}

/// <summary>
/// Reads ProfileList and the loaded HKU hives. Read-only; no profile file is opened here. The
/// elevated helper performs the resolution again and owns any hive load/unload.
/// </summary>
public sealed class DebloatProfileService : IDebloatProfileService
{
    private const string ProfileListPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";
    private static readonly string[] SystemSids = { "S-1-5-18", "S-1-5-19", "S-1-5-20" };

    private readonly IAppLogger? _logger;

    public DebloatProfileService(IAppLogger? logger = null) => _logger = logger;

    public IReadOnlyList<DebloatUserProfile> GetProfiles()
    {
        var profiles = new List<DebloatUserProfile>();
        string currentSid = CurrentSid();

        try
        {
            using RegistryKey? root = Registry.LocalMachine.OpenSubKey(ProfileListPath, writable: false);

            if (root is null)
            {
                return profiles;
            }

            foreach (string sid in root.GetSubKeyNames())
            {
                if (SystemSids.Contains(sid, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                using RegistryKey? key = root.OpenSubKey(sid, writable: false);
                string? path = key?.GetValue("ProfileImagePath") as string;

                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                {
                    continue;
                }

                string name = new DirectoryInfo(path).Name;
                profiles.Add(new DebloatUserProfile(
                    name,
                    sid,
                    path,
                    IsHiveLoaded(sid))
                {
                    IsCurrentUser = sid.Equals(currentSid, StringComparison.OrdinalIgnoreCase),
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.Error("Could not enumerate local profiles.", ex);
        }

        return profiles
            .OrderByDescending(profile => profile.IsCurrentUser)
            .ThenBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public DebloatTargetResolution Resolve(string? token)
        => DebloatTargetResolver.Resolve(
            token,
            GetProfiles(),
            CurrentSid(),
            Environment.OSVersion.Version.Build,
            Path.Combine(
                Environment.GetEnvironmentVariable("SystemDrive") ?? "C:",
                "Users",
                "Default",
                "NTUSER.DAT"));

    private static bool IsHiveLoaded(string sid)
    {
        try
        {
            using RegistryKey? key = Registry.Users.OpenSubKey(sid, writable: false);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string CurrentSid()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.User?.Value ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
