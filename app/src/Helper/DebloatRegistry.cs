using System.Text.Json;
using Microsoft.Win32;
using PCManager.Core.Debloat;

namespace PCManager.Helper;

/// <summary>
/// Registry access for one resolved target. Catalog actions say HKCU; for another user it is
/// mapped to the live HKU\&lt;sid&gt; hive or the helper's loaded HKU\FPM_* mount. The journal
/// keeps the logical HKCU name so undo can replay it against the same target token.
/// </summary>
internal sealed class DebloatRegistry : IDisposable
{
    private readonly DebloatTarget _target;
    private readonly List<RegistryKey> _owned = new();
    private RegistryKey? _userRoot;

    public DebloatRegistry(DebloatTarget target) => _target = target;

    public List<RegistryValueState> Capture(IEnumerable<DebloatAction> actions)
    {
        var states = new List<RegistryValueState>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (DebloatAction action in actions)
        {
            string? name = action switch
            {
                RegistrySetAction set => set.RegistryName,
                RegistryDeleteAction delete => delete.RegistryName,
                _ => null,
            };

            if (name is null || !seen.Add($"{action.Hive}\\{action.Path}\\{name}"))
            {
                continue;
            }

            states.Add(ReadState(action.Hive, action.Path, name));
        }

        return states;
    }

    public RegistryValueState ReadState(string logicalHive, string path, string name)
    {
        using RegistryKey? key = OpenSubKey(logicalHive, path, writable: false);

        if (key is null)
        {
            return new RegistryValueState { Hive = logicalHive, Path = path, Name = name, Existed = false };
        }

        object? value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is null)
        {
            return new RegistryValueState { Hive = logicalHive, Path = path, Name = name, Existed = false };
        }

        RegistryValueKind kind = key.GetValueKind(name);
        string valueType = kind switch
        {
            RegistryValueKind.DWord => "DWord",
            RegistryValueKind.QWord => "QWord",
            RegistryValueKind.MultiString => "MultiString",
            RegistryValueKind.Binary => "Binary",
            RegistryValueKind.ExpandString => "ExpandString",
            _ => "String",
        };

        // The catalog represents Binary as a lower-case hex string. Serializing the byte[] directly
        // would produce base64 and make detect/undo unable to replay the catalog value.
        JsonElement serialized = valueType == "Binary" && value is byte[] bytes
            ? JsonSerializer.SerializeToElement(Convert.ToHexString(bytes).ToLowerInvariant())
            : JsonSerializer.SerializeToElement(value);

        return new RegistryValueState
        {
            Hive = logicalHive,
            Path = path,
            Name = name,
            Existed = true,
            ValueType = valueType,
            Value = serialized,
        };
    }

    public void Execute(DebloatAction action)
    {
        if (action is not (RegistrySetAction or RegistryDeleteAction))
        {
            throw new NotSupportedException("This action is not a registry value change.");
        }

        if (action is RegistryDeleteAction delete)
        {
            using RegistryKey? existing = OpenSubKey(delete.Hive, delete.Path, writable: true);
            existing?.DeleteValue(delete.RegistryName, throwOnMissingValue: false);
            return;
        }

        var set = (RegistrySetAction)action;
        using RegistryKey key = CreateSubKey(set.Hive, set.Path);
        key.SetValue(set.RegistryName, ToClr(set.Value, set.ValueType), ToKind(set.ValueType));
    }

    public bool TryRestore(IReadOnlyList<RegistryValueState> states)
    {
        bool ok = true;

        foreach (RegistryValueState state in states)
        {
            try
            {
                if (!state.Existed)
                {
                    using RegistryKey? key = OpenSubKey(state.Hive, state.Path, writable: true);
                    key?.DeleteValue(state.Name, throwOnMissingValue: false);
                    continue;
                }

                using RegistryKey valueKey = CreateSubKey(state.Hive, state.Path);
                valueKey.SetValue(state.Name, ToClr(state.Value, state.ValueType), ToKind(state.ValueType));
            }
            catch (Exception ex)
            {
                ok = false;
                Log($"Restore failed for {state.Hive}\\{state.Path}\\{state.Name}: {ex.Message}");
            }
        }

        return ok;
    }

    public void Dispose()
    {
        foreach (RegistryKey key in _owned)
        {
            key.Dispose();
        }
    }

    private RegistryKey Root(string logicalHive) => logicalHive switch
    {
        "HKLM" => Registry.LocalMachine,
        "HKCU" when _target.Kind == DebloatTargetKind.CurrentUser => Registry.CurrentUser,
        "HKCU" => _userRoot ??= OpenUserRoot(),
        _ => throw new InvalidOperationException($"Unknown hive '{logicalHive}'."),
    };

    private RegistryKey OpenUserRoot()
    {
        if (!_target.MountRoot.StartsWith(@"HKU\", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The user target has no valid HKU mount.");
        }

        string mount = _target.MountRoot[4..];
        RegistryKey key = Registry.Users.OpenSubKey(mount, writable: true)
            ?? throw new InvalidOperationException($"The target hive {_target.MountRoot} is not available.");

        _owned.Add(key);
        return key;
    }

    private RegistryKey? OpenSubKey(string logicalHive, string path, bool writable) =>
        Root(logicalHive).OpenSubKey(path, writable);

    private RegistryKey CreateSubKey(string logicalHive, string path) =>
        Root(logicalHive).CreateSubKey(path, writable: true);

    private static RegistryValueKind ToKind(string valueType) => valueType switch
    {
        "DWord" => RegistryValueKind.DWord,
        "QWord" => RegistryValueKind.QWord,
        "MultiString" => RegistryValueKind.MultiString,
        "Binary" => RegistryValueKind.Binary,
        "ExpandString" => RegistryValueKind.ExpandString,
        _ => RegistryValueKind.String,
    };

    private static object ToClr(JsonElement? value, string valueType)
    {
        if (value is not { } element)
        {
            return string.Empty;
        }

        return valueType switch
        {
            "DWord" => element.GetUInt32(),
            "QWord" => element.GetUInt64(),
            "MultiString" => element.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray(),
            "Binary" => Convert.FromHexString(element.GetString() ?? string.Empty),
            _ => element.GetString() ?? string.Empty,
        };
    }

    private static void Log(string message)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FarmanullahPCManager",
                "logs");

            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, $"helper-{DateTime.Now:yyyyMMdd}.log"),
                $"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never fail the command.
        }
    }
}
