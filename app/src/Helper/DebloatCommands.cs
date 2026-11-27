using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;
using PCManager.Core.Debloat;
using PCManager.Core.Elevation;

namespace PCManager.Helper;

/// <summary>
/// Catalog-scoped debloat commands. The client can only name a catalog tweak id and a restore
/// state limited to that tweak's own targets; no free-form registry path ever arrives here.
/// </summary>
internal static class DebloatCommands
{
    private const int MaxRestoreEntries = 128;

    private static readonly Lazy<DebloatCatalog> Catalog = new(DebloatCatalogLoader.LoadEmbedded);

    public static HelperResponse Handle(HelperRequest request)
    {
        try
        {
            DebloatTweak? tweak = Catalog.Value.Tweaks
                .FirstOrDefault(candidate => string.Equals(candidate.Id, request.TweakId, StringComparison.Ordinal));

            if (tweak is null)
            {
                return Failure(request, $"Unknown tweak '{request.TweakId}'.");
            }

            return request.Command switch
            {
                HelperCommand.DetectTweak => Detect(request, tweak),
                HelperCommand.ApplyTweak => Apply(request, tweak),
                HelperCommand.RevertTweak => Revert(request, tweak),
                _ => Failure(request, "Unsupported debloat command."),
            };
        }
        catch (Exception ex)
        {
            Log($"Debloat {request.Command} {request.TweakId} failed: {ex}");
            return Failure(request, ex.Message);
        }
    }

    private static HelperResponse Detect(HelperRequest request, DebloatTweak tweak)
    {
        List<RegistryValueState> values = Capture(tweak.Actions);
        bool detected = tweak.Detect.All(probe => Evaluate(probe, values));

        return new HelperResponse
        {
            Success = true,
            Detected = detected,
            Values = values,
            Message = detected ? "Already applied." : "Not applied.",
            RequestId = request.RequestId,
        };
    }

    private static HelperResponse Apply(HelperRequest request, DebloatTweak tweak)
    {
        if (tweak.Actions.Any(action => action is RegistryKeyDeleteAction or AppxRemoveAction))
        {
            return Failure(request, "This tweak's action type is not supported by the registry executor.");
        }

        List<RegistryValueState> before = Capture(tweak.Actions);

        try
        {
            foreach (DebloatAction action in tweak.Actions)
            {
                Execute(action);
            }

            return new HelperResponse
            {
                Success = true,
                Values = before,
                Message = $"Applied '{tweak.Title}'.",
                RequestId = request.RequestId,
            };
        }
        catch (Exception ex)
        {
            bool rolledBack = TryRestore(before);
            Log($"Apply {tweak.Id} failed: {ex.Message}; rolledBack={rolledBack}");
            return new HelperResponse
            {
                Success = false,
                RolledBack = rolledBack,
                Message = $"'{tweak.Title}' failed: {ex.Message}",
                RequestId = request.RequestId,
            };
        }
    }

    private static HelperResponse Revert(HelperRequest request, DebloatTweak tweak)
    {
        if (request.Restore.Count > MaxRestoreEntries)
        {
            return Failure(request, "The restore state is larger than the allowed limit.");
        }

        if (request.Restore.Count > 0)
        {
            string? refusal = ValidateRestore(tweak, request.Restore);
            if (refusal is not null)
            {
                return Failure(request, refusal);
            }

            return TryRestore(request.Restore)
                ? new HelperResponse
                {
                    Success = true,
                    Message = $"Restored '{tweak.Title}' to its previous state.",
                    RequestId = request.RequestId,
                }
                : Failure(request, $"Could not fully restore '{tweak.Title}'.");
        }

        try
        {
            foreach (DebloatAction action in tweak.Revert)
            {
                Execute(action);
            }

            return new HelperResponse
            {
                Success = true,
                Message = $"Applied the reference undo for '{tweak.Title}'.",
                RequestId = request.RequestId,
            };
        }
        catch (Exception ex)
        {
            return Failure(request, $"Revert failed: {ex.Message}");
        }
    }

    private static List<RegistryValueState> Capture(IEnumerable<DebloatAction> actions)
    {
        var states = new List<RegistryValueState>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (DebloatAction action in actions)
        {
            if (action is not (RegistrySetAction or RegistryDeleteAction))
            {
                continue;
            }

            string key = $"{action.Hive}\\{action.Path}\\{action.Name}";
            if (!seen.Add(key))
            {
                continue;
            }

            states.Add(ReadState(action.Hive, action.Path, action.Name));
        }

        return states;
    }

    private static RegistryValueState ReadState(string hive, string path, string name)
    {
        using RegistryKey? key = OpenHive(hive)?.OpenSubKey(path, writable: false);

        if (key is null)
        {
            return new RegistryValueState { Hive = hive, Path = path, Name = name, Existed = false };
        }

        object? value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is null)
        {
            return new RegistryValueState { Hive = hive, Path = path, Name = name, Existed = false };
        }

        RegistryValueKind kind = key.GetValueKind(name);
        return new RegistryValueState
        {
            Hive = hive,
            Path = path,
            Name = name,
            Existed = true,
            ValueType = kind switch
            {
                RegistryValueKind.DWord => "DWord",
                RegistryValueKind.QWord => "QWord",
                RegistryValueKind.MultiString => "MultiString",
                RegistryValueKind.Binary => "Binary",
                RegistryValueKind.ExpandString => "ExpandString",
                _ => "String",
            },
            Value = JsonSerializer.SerializeToElement(value),
        };
    }

    private static void Execute(DebloatAction action)
    {
        if (action is OptionalFeatureAction feature)
        {
            SetOptionalFeature(feature.FeatureName, feature.Enable);
            return;
        }

        if (action is AppxRemoveAction)
        {
            throw new NotSupportedException("App package removal is not part of the registry executor.");
        }

        if (action is RegistryKeyDeleteAction)
        {
            throw new NotSupportedException("Key deletion is not supported by this engine yet.");
        }

        using RegistryKey hive = OpenHive(action.Hive)
            ?? throw new InvalidOperationException($"Unknown hive '{action.Hive}'.");

        if (action is RegistryDeleteAction delete)
        {
            using RegistryKey? existing = hive.OpenSubKey(delete.Path, writable: true);
            existing?.DeleteValue(delete.RegistryName, throwOnMissingValue: false);
            return;
        }

        var set = (RegistrySetAction)action;
        using RegistryKey key = hive.CreateSubKey(set.Path, writable: true);
        key.SetValue(set.RegistryName, ToClr(set.Value, set.ValueType), ToKind(set.ValueType));
    }

    private static bool TryRestore(IReadOnlyList<RegistryValueState> states)
    {
        bool ok = true;

        foreach (RegistryValueState state in states)
        {
            try
            {
                using RegistryKey hive = OpenHive(state.Hive)!;

                if (!state.Existed)
                {
                    using RegistryKey? key = hive.OpenSubKey(state.Path, writable: true);
                    key?.DeleteValue(state.Name, throwOnMissingValue: false);
                    continue;
                }

                using RegistryKey key = hive.CreateSubKey(state.Path, writable: true);
                key.SetValue(state.Name, ToClr(state.Value, state.ValueType), ToKind(state.ValueType));
            }
            catch (Exception ex)
            {
                ok = false;
                Log($"Restore failed for {state.Hive}\\{state.Path}\\{state.Name}: {ex.Message}");
            }
        }

        return ok;
    }

    private static string? ValidateRestore(DebloatTweak tweak, IReadOnlyList<RegistryValueState> restore)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DebloatAction action in tweak.Actions)
        {
            if (action is RegistrySetAction set)
            {
                allowed.Add($"{set.Hive}\\{set.Path}\\{set.RegistryName}");
            }
            else if (action is RegistryDeleteAction delete)
            {
                allowed.Add($"{delete.Hive}\\{delete.Path}\\{delete.RegistryName}");
            }
        }

        foreach (RegistryValueState state in restore)
        {
            if (!allowed.Contains($"{state.Hive}\\{state.Path}\\{state.Name}"))
            {
                return $"Restore value '{state.Name}' is not a target of '{tweak.Id}'.";
            }

            if (!DebloatCatalogValidator.AllowedHives.Contains(state.Hive) ||
                (state.Hive == "HKU" && !state.Path.StartsWith("Default", StringComparison.OrdinalIgnoreCase)))
            {
                return "Restore state uses a hive outside the allow-list.";
            }
        }

        return null;
    }

    private static bool Evaluate(DebloatProbe probe, IReadOnlyList<RegistryValueState> values) => probe switch
    {
        RegistryEqualsProbe equals => values.Any(state =>
            state.Hive == equals.Hive &&
            state.Path == equals.Path &&
            state.Name == equals.RegistryName &&
            state.Existed &&
            state.ValueType == equals.ValueType &&
            JsonElement.DeepEquals(state.Value, equals.Value)),
        RegistryAbsentProbe absent => values.Any(state =>
            state.Hive == absent.Hive &&
            state.Path == absent.Path &&
            state.Name == absent.RegistryName &&
            !state.Existed),
        OptionalFeatureProbe feature => IsFeatureEnabled(feature.FeatureName) == feature.ExpectEnabled,
        _ => false,
    };

    private static bool IsFeatureEnabled(string name)
    {
        ProcessResult result = RunDism($"/online /get-featureinfo /featurename:{name}");
        return result.Output.Contains("State : Enabled", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetOptionalFeature(string name, bool enable)
    {
        string verb = enable ? "/enable-feature" : "/disable-feature";
        ProcessResult result = RunDism($"/online {verb} /featurename:{name} /all /norestart");
        if (result.ExitCode != 0 && result.ExitCode != 3010)
        {
            throw new InvalidOperationException(
                $"DISM {verb} {name} failed with exit code {result.ExitCode}.");
        }
    }

    private static ProcessResult RunDism(string arguments)
    {
        var startInfo = new ProcessStartInfo("dism.exe", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows did not start DISM.");

        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit(120000);
        return new ProcessResult(process.ExitCode, output);
    }

    private static RegistryKey? OpenHive(string hive) => hive switch
    {
        "HKCU" => Registry.CurrentUser,
        "HKLM" => Registry.LocalMachine,
        "HKU" => Registry.Users,
        _ => null,
    };

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

    private static HelperResponse Failure(HelperRequest request, string message) => new()
    {
        Success = false,
        Message = message,
        RequestId = request.RequestId,
    };

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
            // The helper must not fail because its own log is unavailable.
        }
    }

    private readonly record struct ProcessResult(int ExitCode, string Output);
}
