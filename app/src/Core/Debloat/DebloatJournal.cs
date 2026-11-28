using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PCManager.Core.Debloat;

/// <summary>One applied tweak with the exact pre-state captured before it ran.</summary>
public sealed class DebloatJournalEntry
{
    public string TweakId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime AppliedAtUtc { get; set; }
    public List<RegistryValueState> Before { get; set; } = new();
}

/// <summary>One apply run. Saved after every tweak so a crash leaves a truthful record.</summary>
public sealed class DebloatRunRecord
{
    public string RunId { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public bool Completed { get; set; }
    public bool RolledBack { get; set; }
    public bool Undone { get; set; }
    public string FailureMessage { get; set; } = string.Empty;
    public string RestorePointStatus { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>Typed user target token; empty means the signed-in account (old journals).</summary>
    public string TargetUser { get; set; } = string.Empty;
    public bool RequiresReboot { get; set; }
    public bool RequiresExplorerRestart { get; set; }
    public List<DebloatJournalEntry> Entries { get; set; } = new();
}

/// <summary>Appends/rereads run records under %LOCALAPPDATA%\FPM\debloat\journal.</summary>
public sealed class DebloatJournalStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public DebloatJournalStore(string? directory = null)
    {
        Directory = directory ?? System.IO.Path.Combine(AppInfo.DataFolder, "debloat", "journal");
    }

    public string Directory { get; }

    public void Save(DebloatRunRecord record)
    {
        System.IO.Directory.CreateDirectory(Directory);
        string path = System.IO.Path.Combine(Directory, $"{record.RunId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(record, Options));
    }

    /// <summary>Every saved run, newest first; the page shows a bounded history.</summary>
    public IReadOnlyList<DebloatRunRecord> LoadAll(int limit = 20)
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return Array.Empty<DebloatRunRecord>();
        }

        var records = new List<DebloatRunRecord>();

        foreach (string path in System.IO.Directory.EnumerateFiles(Directory, "*.json")
                     .OrderByDescending(File.GetLastWriteTimeUtc)
                     .Take(limit))
        {
            try
            {
                if (JsonSerializer.Deserialize<DebloatRunRecord>(File.ReadAllText(path), Options) is { } record)
                {
                    records.Add(record);
                }
            }
            catch (JsonException)
            {
                // A half-written record must not hide the rest of the history.
            }
        }

        return records;
    }

    public DebloatRunRecord? LoadLatest() => LoadAll(limit: 1).FirstOrDefault();
}

/// <summary>Builds a .reg text backup of every previous value the run will touch.</summary>
public static class DebloatRegBackup
{
    public static string Build(IEnumerable<RegistryValueState> states, DateTime createdAtUtc)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Windows Registry Editor Version 5.00");
        builder.AppendLine($"; FPM debloat backup {createdAtUtc:yyyy-MM-dd HH:mm:ss} UTC");

        foreach (IGrouping<(string Hive, string Path), RegistryValueState> group in states
                     .Where(state => state.Existed)
                     .GroupBy(state => (state.Hive, state.Path)))
        {
            string full = group.Key.Hive switch
            {
                "HKLM" => @"HKEY_LOCAL_MACHINE\" + group.Key.Path,
                "HKU" => @"HKEY_USERS\" + group.Key.Path,
                _ => @"HKEY_CURRENT_USER\" + group.Key.Path,
            };

            builder.AppendLine();
            builder.AppendLine($"[{full}]");

            foreach (RegistryValueState state in group)
            {
                builder.AppendLine($"\"{state.Name.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"={Literal(state)}");
            }
        }

        return builder.ToString();
    }

    private static string Literal(RegistryValueState state)
    {
        JsonElement? value = state.Value;

        return state.ValueType switch
        {
            "DWord" => $"dword:{ToUInt(value):x8}",
            "QWord" => $"hex(b):{ToUInt64(value):x16}",
            "MultiString" => $"hex(7):{Hex(Utf16(MultiString(value)))}",
            "Binary" => $"hex:{Hex(Binary(value))}",
            "ExpandString" => $"hex(2):{Hex(Utf16(StringValue(value) + "\0"))}",
            _ => $"\"{StringValue(value).Replace("\\", "\\\\").Replace("\"", "\\\"")}\"",
        };
    }

    private static string StringValue(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.String } element ? element.GetString() ?? string.Empty : string.Empty;

    private static string MultiString(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.Array } element
            ? string.Join("\0", element.EnumerateArray().Select(item => item.GetString() ?? string.Empty)) + "\0"
            : string.Empty;

    private static byte[] Binary(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.String } element
            ? Convert.FromHexString(element.GetString() ?? string.Empty)
            : Array.Empty<byte>();

    private static byte[] Utf16(string text) => Encoding.Unicode.GetBytes(text);

    private static string Hex(byte[] bytes) => string.Join(",", bytes.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));

    private static uint ToUInt(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.Number } element && element.TryGetUInt32(out uint number) ? number : 0;

    private static ulong ToUInt64(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.Number } element && element.TryGetUInt64(out ulong number) ? number : 0;
}
