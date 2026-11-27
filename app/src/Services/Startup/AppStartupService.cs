using Microsoft.Win32;
using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Startup;

/// <summary>
/// Per-user "Start with Windows" registration.
///
/// HKCU\...\Run is read by Explorer at logon. We use the value name
/// <see cref="AppInfo.RunValueName"/> and the quoted full path of the current executable,
/// which is exactly what the Windows "Apps > Startup" page displays and can disable.
/// </summary>
public sealed class AppStartupService : IAppStartupService
{
    public bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(AppInfo.RunKeyPath, writable: false);
            return key?.GetValue(AppInfo.RunValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public OperationResult SetEnabled(bool enabled) => SetEnabled(enabled, toolbarOnly: false);

    public OperationResult SetEnabled(bool enabled, bool toolbarOnly)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(AppInfo.RunKeyPath, writable: true);

            if (!enabled)
            {
                if (key.GetValue(AppInfo.RunValueName) is not null)
                {
                    key.DeleteValue(AppInfo.RunValueName, throwOnMissingValue: false);
                }

                return OperationResult.Ok($"{AppInfo.ProductName} will no longer start with Windows.");
            }

            string? executable = Environment.ProcessPath;

            if (string.IsNullOrWhiteSpace(executable))
            {
                return OperationResult.Fail("The executable path could not be resolved.");
            }

            // Quoted so paths containing spaces start correctly at logon; the toolbar-only switch
            // keeps the sign-in footprint to tray + toolbar when the toolbar was left running.
            string command = toolbarOnly ? $"\"{executable}\" --toolbar-only" : $"\"{executable}\"";
            key.SetValue(AppInfo.RunValueName, command, RegistryValueKind.String);

            return OperationResult.Ok($"{AppInfo.ProductName} will now start with Windows.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }
}
