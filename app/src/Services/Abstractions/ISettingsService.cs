using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Loads/saves <see cref="AppSettings"/> as JSON under %LOCALAPPDATA%.</summary>
public interface ISettingsService
{
    AppSettings Current { get; }
    void Save();
}
