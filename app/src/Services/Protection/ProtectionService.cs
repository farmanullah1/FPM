using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Protection;

/// <summary>Protection page facade over the Defender and Update status providers.</summary>
public sealed class ProtectionService : IProtectionService
{
    private readonly IDefenderStatusService _defender;
    private readonly IWindowsUpdateStatusService _update;

    public ProtectionService(IDefenderStatusService defender, IWindowsUpdateStatusService update)
    {
        _defender = defender;
        _update = update;
    }

    public DefenderStatus GetDefenderStatus() => _defender.GetStatus();

    public UpdateStatus GetUpdateStatus() => _update.GetStatus();
}
