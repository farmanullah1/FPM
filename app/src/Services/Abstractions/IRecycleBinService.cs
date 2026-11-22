namespace PCManager.Services.Abstractions;

/// <summary>Recycle bin size query and empty operation (shell32).</summary>
public interface IRecycleBinService
{
    long GetSize();
    long Empty();
}
