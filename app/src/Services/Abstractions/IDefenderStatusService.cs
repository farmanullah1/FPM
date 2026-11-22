using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Defender state. Primary source is the Defender WMI provider
/// (root\Microsoft\Windows\Defender, MSFT_MpComputerStatus); the SecurityCenter2
/// AntiVirusProduct class is used as a fallback.
/// </summary>
public interface IDefenderStatusService
{
    DefenderStatus GetStatus();
}
