using System.Text.Json;

namespace PCManager.Core.Debloat;

/// <summary>
/// One registry value's exact state before a change, as captured by the elevated helper. The
/// journal stores these and undo re-applies them verbatim ("did not exist" → delete).
/// </summary>
public sealed class RegistryValueState
{
    public string Hive { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Existed { get; set; }
    public string ValueType { get; set; } = string.Empty;
    public JsonElement? Value { get; set; }
}

/// <summary>Typed result of one helper debloat command.</summary>
public sealed class DebloatHelperOutcome
{
    public bool Success { get; init; }
    public bool Cancelled { get; init; }
    public bool RolledBack { get; init; }
    public bool? Detected { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<RegistryValueState> Values { get; init; } = Array.Empty<RegistryValueState>();

    public static DebloatHelperOutcome Failed(string message, bool cancelled = false) =>
        new() { Success = false, Cancelled = cancelled, Message = message };
}
