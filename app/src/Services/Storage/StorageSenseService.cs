using Microsoft.Win32;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Storage;

/// <summary>
/// Storage Sense switch.
///
/// Windows stores the toggle as a DWORD named "01" under
/// HKCU\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StorageSense.
/// Writing it takes effect immediately for new schedules; the Settings app reads the
/// same value, so the two stay in sync.
/// </summary>
public sealed class StorageSenseService : IStorageSenseService
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StorageSense";
    private const string ValueName = "01";

    public bool GetEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(KeyPath);
            object? value = key?.GetValue(ValueName);
            return value is int flag && flag != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not open the Storage Sense registry key.");

        key.SetValue(ValueName, enabled ? 1 : 0, RegistryValueKind.DWord);
    }
}
