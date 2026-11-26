using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Loads/saves <see cref="AppSettings"/> as JSON under %LOCALAPPDATA%.</summary>
public interface ISettingsService
{
    AppSettings Current { get; }

    /// <summary>True when no settings file existed at load, so this is the first run.</summary>
    bool IsFirstRun { get; }

    void Save();
}
