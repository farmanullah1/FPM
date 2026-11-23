using PCManager.Core;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Infrastructure;

public sealed class LegacyIdentityMigrationService : ILegacyIdentityMigrationService
{
    private readonly IAppLogger _logger;

    public LegacyIdentityMigrationService(IAppLogger logger)
    {
        _logger = logger;
    }

    public bool HasLegacyState()
    {
        try
        {
            string legacyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppInfo.LegacyDataFolderName);

            return Directory.Exists(legacyPath);
        }
        catch
        {
            return false;
        }
    }

    public MigrationResult MigrateIfNecessary()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string legacyPath = Path.Combine(localAppData, AppInfo.LegacyDataFolderName);
        string newPath = AppInfo.DataFolder;

        if (!Directory.Exists(legacyPath))
        {
            return new MigrationResult(false, legacyPath, newPath, 0, 0, true, "No legacy data found.");
        }

        try
        {
            Directory.CreateDirectory(newPath);

            int copied = 0;
            long bytes = 0;

            // Migrate safe config and history files: settings.json, cleanup_history.json, whitelist.json
            string[] allowedFiles = { "settings.json", "cleanup_history.json", "whitelist.json" };
            foreach (string file in allowedFiles)
            {
                string sourceFile = Path.Combine(legacyPath, file);
                string targetFile = Path.Combine(newPath, file);

                if (File.Exists(sourceFile) && !File.Exists(targetFile))
                {
                    File.Copy(sourceFile, targetFile, overwrite: false);
                    copied++;
                    bytes += new FileInfo(sourceFile).Length;
                    _logger.Info($"Migrated {file} from {legacyPath} to {newPath}");
                }
            }

            // Copy logs if not existing
            string legacyLogs = Path.Combine(legacyPath, "logs");
            string newLogs = AppInfo.LogFolder;
            if (Directory.Exists(legacyLogs))
            {
                Directory.CreateDirectory(newLogs);
                foreach (string logFile in Directory.GetFiles(legacyLogs, "*.log"))
                {
                    string target = Path.Combine(newLogs, Path.GetFileName(logFile));
                    if (!File.Exists(target))
                    {
                        File.Copy(logFile, target, overwrite: false);
                        copied++;
                        bytes += new FileInfo(logFile).Length;
                    }
                }
            }

            return new MigrationResult(true, legacyPath, newPath, copied, bytes, true, "Legacy data successfully migrated.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to migrate legacy data: {ex.Message}", ex);
            return new MigrationResult(true, legacyPath, newPath, 0, 0, false, ex.Message);
        }
    }
}
