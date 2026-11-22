using Microsoft.Win32;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Toolbox;

/// <summary>
/// Reads the current default browser.
///
/// Windows 10/11 stores the user's choice in the UserChoice key:
///   HKCU\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice
///   value ProgId = e.g. "MSEdgeHTM"
/// The ProgId is resolved to an executable through
///   HKCR\&lt;ProgId&gt;\shell\open\command
/// Changing the default programmatically is blocked by Windows (UserChoice is
/// hash-protected), so the supported action is opening the Default apps page.
/// </summary>
public sealed class DefaultBrowserService : IDefaultBrowserService
{
    private const string UserChoiceKey =
        @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice";

    public DefaultBrowserSetting GetDefaultBrowser()
    {
        string progId = string.Empty;

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(UserChoiceKey);
            progId = key?.GetValue("ProgId") as string ?? string.Empty;
        }
        catch (Exception) { }

        var setting = new DefaultBrowserSetting
        {
            ProgId = progId,
            IsDefault = !string.IsNullOrEmpty(progId)
        };

        if (string.IsNullOrEmpty(progId))
        {
            setting.Name = "Not configured";
            return setting;
        }

        string command = string.Empty;

        try
        {
            using RegistryKey? commandKey = Registry.ClassesRoot.OpenSubKey($"{progId}\\shell\\open\\command");
            command = commandKey?.GetValue(null) as string ?? string.Empty;
        }
        catch (Exception) { }

        setting.ExecutablePath = ExtractExecutable(command);
        setting.Name = FriendlyName(progId, setting.ExecutablePath);
        return setting;
    }

    public OperationResult OpenDefaultAppsSettings()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:defaultapps")
            {
                UseShellExecute = true
            });

            return OperationResult.Ok("Opened Default apps. Pick your browser there - Windows blocks changing it from other programs.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    private static string ExtractExecutable(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            int closing = command.IndexOf('"', 1);
            return closing > 0 ? command[1..closing] : command.Trim('"');
        }

        int space = command.IndexOf(' ');
        return space > 0 ? command[..space] : command;
    }

    private static string FriendlyName(string progId, string executable)
    {
        string fileName = string.Empty;

        try
        {
            if (!string.IsNullOrEmpty(executable))
            {
                fileName = System.IO.Path.GetFileNameWithoutExtension(executable);
            }
        }
        catch (ArgumentException) { }

        string haystack = $"{progId} {fileName}".ToUpperInvariant();

        if (haystack.Contains("MSEDGE") || haystack.Contains("MICROSOFTEDGE")) return "Microsoft Edge";
        if (haystack.Contains("CHROME")) return "Google Chrome";
        if (haystack.Contains("FIREFOX")) return "Mozilla Firefox";
        if (haystack.Contains("BRAVE")) return "Brave";
        if (haystack.Contains("OPERA")) return "Opera";
        if (haystack.Contains("VIVALDI")) return "Vivaldi";

        return string.IsNullOrEmpty(fileName) ? progId : fileName;
    }
}
