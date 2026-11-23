namespace PCManager.Services.Abstractions;

/// <summary>
/// Result of evaluating or performing legacy identity data migration (FarmanullahPCManager -> FPM).
/// </summary>
public sealed record MigrationResult(
    bool LegacyDataDetected,
    string LegacyPath,
    string NewPath,
    int FilesCopied,
    long BytesMigrated,
    bool IsCompleted,
    string? Message = null);

/// <summary>
/// Detects, copies and validates user settings and history from the legacy FarmanullahPCManager folder
/// into the canonical %LOCALAPPDATA%\FPM directory without destroying the legacy source rollback copy.
/// </summary>
public interface ILegacyIdentityMigrationService
{
    /// <summary>Checks whether legacy directory or registry keys exist.</summary>
    bool HasLegacyState();

    /// <summary>Performs preflight checks and copies allowed user data to FPM data directory.</summary>
    MigrationResult MigrateIfNecessary();
}
