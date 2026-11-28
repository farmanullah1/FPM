using System.Globalization;
using System.Text.RegularExpressions;

namespace PCManager.Core.Android;

public enum AdbCommandKind
{
    StartServer,
    KillServer,
    Devices,
    GetProperties,
    ListUsers,
    ListPackages,
    UninstallPackage,
    DisablePackage,
    EnablePackage,
    RestorePackage,
}

/// <summary>
/// The complete typed ADB surface. Serials, package ids and users are data, never shell text; the
/// builder emits an argument list and rejects malformed values instead of concatenating a command.
/// </summary>
public sealed record AdbCommand(
    AdbCommandKind Kind,
    string Serial = "",
    string PackageId = "",
    int UserId = -1,
    PackageListFilter ListFilter = PackageListFilter.All)
{
    public static AdbCommand StartServer() => new(AdbCommandKind.StartServer);
    public static AdbCommand KillServer() => new(AdbCommandKind.KillServer);
    public static AdbCommand Devices() => new(AdbCommandKind.Devices);
    public static AdbCommand GetProperties(string serial) => new(AdbCommandKind.GetProperties, serial);
    public static AdbCommand ListUsers(string serial) => new(AdbCommandKind.ListUsers, serial);
    public static AdbCommand ListPackages(string serial, int userId, PackageListFilter filter) =>
        new(AdbCommandKind.ListPackages, serial, UserId: userId, ListFilter: filter);
    public static AdbCommand UninstallPackage(string serial, string packageId, int userId) =>
        new(AdbCommandKind.UninstallPackage, serial, packageId, userId);
    public static AdbCommand DisablePackage(string serial, string packageId, int userId) =>
        new(AdbCommandKind.DisablePackage, serial, packageId, userId);
    public static AdbCommand EnablePackage(string serial, string packageId, int userId) =>
        new(AdbCommandKind.EnablePackage, serial, packageId, userId);
    public static AdbCommand RestorePackage(string serial, string packageId, int userId) =>
        new(AdbCommandKind.RestorePackage, serial, packageId, userId);
}

public static class AdbCommandLine
{
    private static readonly Regex SerialPattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PackagePattern = new(
        @"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z0-9_]+)+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsValidSerial(string serial) =>
        !string.IsNullOrWhiteSpace(serial) && SerialPattern.IsMatch(serial);

    public static bool IsValidPackageId(string packageId) =>
        !string.IsNullOrWhiteSpace(packageId) && PackagePattern.IsMatch(packageId);

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
            case AdbCommandKind.ListPackages:
                RequireSerial(command.Serial);
                RequireUser(command.UserId);
                return ListPackages(command);
            case AdbCommandKind.UninstallPackage:
                RequirePackageCommand(command);
                return new[]
                {
                    "-s", command.Serial, "shell", "pm", "uninstall", "-k",
                    "--user", command.UserId.ToString(CultureInfo.InvariantCulture), command.PackageId,
                };
            case AdbCommandKind.DisablePackage:
                RequirePackageCommand(command);
                return new[]
                {
                    "-s", command.Serial, "shell", "pm", "disable-user",
                    "--user", command.UserId.ToString(CultureInfo.InvariantCulture), command.PackageId,
                };
            case AdbCommandKind.EnablePackage:
                RequirePackageCommand(command);
                return new[]
                {
                    "-s", command.Serial, "shell", "pm", "enable",
                    "--user", command.UserId.ToString(CultureInfo.InvariantCulture), command.PackageId,
                };
            case AdbCommandKind.RestorePackage:
                RequirePackageCommand(command);
                return new[]
                {
                    "-s", command.Serial, "shell", "cmd", "package", "install-existing",
                    "--user", command.UserId.ToString(CultureInfo.InvariantCulture), command.PackageId,
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(command), "Unsupported ADB command.");
        }
    }

    private static IReadOnlyList<string> ListPackages(AdbCommand command)
    {
        var arguments = new List<string>
        {
            "-s", command.Serial, "shell", "pm", "list", "packages",
        };

        switch (command.ListFilter)
        {
            case PackageListFilter.All:
                arguments.Add("-u");
                break;
            case PackageListFilter.System:
                arguments.Add("-s");
                arguments.Add("-u");
                break;
            case PackageListFilter.Enabled:
                arguments.Add("-e");
                break;
            case PackageListFilter.Disabled:
                arguments.Add("-d");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), "Unsupported package filter.");
        }

        arguments.Add("--user");
        arguments.Add(command.UserId.ToString(CultureInfo.InvariantCulture));
        return arguments;
    }

    private static void RequirePackageCommand(AdbCommand command)
    {
        RequireSerial(command.Serial);
        RequireUser(command.UserId);

        if (!IsValidPackageId(command.PackageId))
        {
            throw new ArgumentException("Android package id is malformed.", nameof(command));
        }
    }

    private static void RequireUser(int userId)
    {
        if (userId < 0)
        {
            throw new ArgumentException("Android user id must be zero or greater.", "userId");
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
