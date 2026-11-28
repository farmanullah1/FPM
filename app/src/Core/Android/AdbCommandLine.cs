using System.Text.RegularExpressions;

namespace PCManager.Core.Android;

public enum AdbCommandKind
{
    StartServer,
    KillServer,
    Devices,
    GetProperties,
    ListUsers,
}

/// <summary>
/// A1's complete command surface. A serial is data, never shell text; the builder emits an
/// argument list and rejects malformed serials instead of concatenating a command line.
/// </summary>
public sealed record AdbCommand(AdbCommandKind Kind, string Serial = "")
{
    public static AdbCommand StartServer() => new(AdbCommandKind.StartServer);
    public static AdbCommand KillServer() => new(AdbCommandKind.KillServer);
    public static AdbCommand Devices() => new(AdbCommandKind.Devices);
    public static AdbCommand GetProperties(string serial) => new(AdbCommandKind.GetProperties, serial);
    public static AdbCommand ListUsers(string serial) => new(AdbCommandKind.ListUsers, serial);
}

public static class AdbCommandLine
{
    private static readonly Regex SerialPattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsValidSerial(string serial) =>
        !string.IsNullOrWhiteSpace(serial) && SerialPattern.IsMatch(serial);

    public static IReadOnlyList<string> Build(AdbCommand command)
    {
        switch (command.Kind)
        {
            case AdbCommandKind.StartServer:
                return new[] { "start-server" };
            case AdbCommandKind.KillServer:
                return new[] { "kill-server" };
            case AdbCommandKind.Devices:
                return new[] { "devices", "-l" };
            case AdbCommandKind.GetProperties:
                RequireSerial(command.Serial);
                return new[] { "-s", command.Serial, "shell", "getprop" };
            case AdbCommandKind.ListUsers:
                RequireSerial(command.Serial);
                return new[] { "-s", command.Serial, "shell", "pm", "list", "users" };
            default:
                throw new ArgumentOutOfRangeException(nameof(command), "Unsupported ADB command.");
        }
    }

    private static void RequireSerial(string serial)
    {
        if (!IsValidSerial(serial))
        {
            throw new ArgumentException("ADB serial is malformed.", nameof(serial));
        }
    }
}
