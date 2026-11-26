using System.Text.Json;
using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Settings;

/// <summary>
/// JSON settings store at %LOCALAPPDATA%\FarmanullahPCManager\settings.json.
/// The folder is created on first use; corrupt or missing files fall back to defaults
/// instead of failing start-up. Nothing is ever written under a Microsoft folder.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _path;

    public SettingsService()
    {
        Directory.CreateDirectory(AppInfo.DataFolder);
        _path = AppInfo.SettingsFile;
        IsFirstRun = !File.Exists(_path);
        Current = Load();
    }

    public AppSettings Current { get; private set; }

    public bool IsFirstRun { get; }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppInfo.DataFolder);
            File.WriteAllText(_path, JsonSerializer.Serialize(Current, SerializerOptions));
        }
        catch (Exception)
        {
            // A read-only profile must not crash the app.
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings();
            }
        }
        catch (Exception) { }

        return new AppSettings();
    }
}
