namespace PCManager.Services.Abstractions;

/// <summary>
/// Storage Sense state, stored under
/// HKCU\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StorageSense
/// as the DWORD value "01".
/// </summary>
public interface IStorageSenseService
{
    bool GetEnabled();
    void SetEnabled(bool enabled);
}
