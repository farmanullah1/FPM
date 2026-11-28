namespace PCManager.Core.Debloat;

/// <summary>
/// Validates journal pre-state before it reaches the elevated helper. Only the tweak's own
/// registry targets and the catalog hive/path allow-list are accepted; unknown ids are rejected
/// by the helper's catalog lookup before this guard runs.
/// </summary>
public static class DebloatRestoreGuard
{
    public static string? Validate(DebloatTweak tweak, IReadOnlyList<RegistryValueState> restore)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (DebloatAction action in tweak.Actions)
        {
            switch (action)
            {
                case RegistrySetAction set:
                    allowed.Add($"{set.Hive}\\{set.Path}\\{set.RegistryName}");
                    break;
                case RegistryDeleteAction delete:
                    allowed.Add($"{delete.Hive}\\{delete.Path}\\{delete.RegistryName}");
                    break;
            }
        }

        foreach (RegistryValueState state in restore)
        {
            if (!allowed.Contains($"{state.Hive}\\{state.Path}\\{state.Name}"))
            {
                return $"Restore value '{state.Name}' is not a target of '{tweak.Id}'.";
            }

            if (!DebloatCatalogValidator.AllowedHives.Contains(state.Hive) ||
                (state.Hive == "HKU" &&
                 !state.Path.StartsWith("Default", StringComparison.OrdinalIgnoreCase)))
            {
                return "Restore state uses a hive outside the allow-list.";
            }
        }

        return null;
    }
}
