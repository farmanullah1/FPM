using System.Security.Principal;
using Microsoft.Win32;
using PCManager.Core.Debloat;

namespace PCManager.Helper;

/// <summary>Resolves the client's typed target against the machine's own ProfileList.</summary>
internal static class HelperHive
{
    private const string ProfileListPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";
    private static readonly string[] SystemSids = { "S-1-5-18", "S-1-5-19", "S-1-5-20" };

    public static DebloatTargetResolution Resolve(string? token)
    {
        string currentSid = CurrentSid();
        var profiles = new List<DebloatUserProfile>();

        using (RegistryKey? root = Registry.LocalMachine.OpenSubKey(ProfileListPath, writable: false))
        {
            if (root is not null)
            {
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

                    profiles.Add(new DebloatUserProfile(
                        new DirectoryInfo(path).Name,
                        sid,
                        path,
                        IsLoaded(sid))
                    {
                        IsCurrentUser = sid.Equals(currentSid, StringComparison.OrdinalIgnoreCase),
                    });
                }
            }
        }

        return DebloatTargetResolver.Resolve(
            token,
            profiles,
            currentSid,
            Environment.OSVersion.Version.Build,
            Path.Combine(
                Environment.GetEnvironmentVariable("SystemDrive") ?? "C:",
                "Users",
                "Default",
                "NTUSER.DAT"));
    }

    public static string CurrentSid()
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

    private static bool IsLoaded(string sid)
    {
        using RegistryKey? key = Registry.Users.OpenSubKey(sid, writable: false);
        return key is not null;
    }
}

/// <summary>RegLoadKey/RegUnloadKey with a bounded retry while Windows releases the hive.</summary>
internal sealed class RegistryHiveApi : IDebloatHiveApi
{
    private static readonly IntPtr HkeyUsers = unchecked((IntPtr)(int)0x80000003);

    public bool IsLoaded(string mountKey)
    {
        using RegistryKey? key = Registry.Users.OpenSubKey(mountKey, writable: false);
        return key is not null;
    }

    public void Load(string mountKey, string hiveFile)
    {
        int result = RegLoadKey(HkeyUsers, mountKey, hiveFile);

        if (result != 0)
        {
            throw new InvalidOperationException(
                $"RegLoadKey failed for {mountKey} with error {result}.");
        }
    }

    public void Unload(string mountKey)
    {
        int last = 0;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            last = RegUnloadKey(HkeyUsers, mountKey);

            if (last == 0)
            {
                return;
            }

            // AV scanners and the shell can hold the hive briefly; retry, then fail honestly.
            Thread.Sleep(200);
        }

        throw new InvalidOperationException(
            $"RegUnloadKey failed for {mountKey} with error {last} after retries.");
    }

    [System.Runtime.InteropServices.DllImport(
        "advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern int RegLoadKey(IntPtr hKey, string subKey, string file);

    [System.Runtime.InteropServices.DllImport(
        "advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern int RegUnloadKey(IntPtr hKey, string subKey);
}
