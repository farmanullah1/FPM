using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Phase 2 gate. Accepts exactly <c>proceed</c>, <c>only 1, 2</c> and <c>keep 1, 2</c>
/// (single numbers or ranges such as <c>only 3-7</c>). Everything else returns a
/// clarification and no plan, so no deletion can start from an unrecognised command.
/// </summary>
public interface ICleanupCommandParser
{
    CleanupCommandResult Parse(string command, CleanupScanResult scan);
}
