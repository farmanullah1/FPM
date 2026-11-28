using PCManager.Core.Android;
using Xunit;

namespace PCManager.Tests;

public sealed class AdbParserTests
{
    [Fact]
    public void Devices_parser_handles_authorized_wireless_emulator_unauthorized_offline_and_no_permissions()
    {
        const string output =
            "List of devices attached\n" +
            "R58M123456 device product:beyond1q model:SM_G973F device:beyond1q transport_id:3\n" +
            "emulator-5554 device product:sdk_gphone_x86 model:sdk_gphone_x86 device:generic_x86 transport_id:1\n" +
            "4C:12:34 unauthorized usb:1-2 transport_id:4\n" +
            "???????????? no permissions (user in plugdev group); see [http://developer.android.com/tools/device.html]\n" +
            "192.168.1.7:5555 offline transport_id:5\n";

        IReadOnlyList<AdbDevice> devices = AdbParsers.ParseDevices(output);

        Assert.Equal(5, devices.Count);
        Assert.Equal(AdbDeviceState.Device, devices[0].State);
        Assert.Equal("SM_G973F", devices[0].Model);
        Assert.Equal(AdbConnectionKind.Usb, devices[0].Connection);
        Assert.Equal(AdbConnectionKind.Emulator, devices[1].Connection);
        Assert.Equal(AdbDeviceState.Unauthorized, devices[2].State);
        Assert.Equal(AdbDeviceState.NoPermissions, devices[3].State);
        Assert.Equal(AdbDeviceState.Offline, devices[4].State);
        Assert.Equal(AdbConnectionKind.Wireless, devices[4].Connection);
    }

    [Fact]
    public void Getprop_and_users_are_parsed_without_guessing_unavailable_values()
    {
        const string props =
            "[ro.product.brand]: [Google]\n" +
            "[ro.product.manufacturer]: [Google]\n" +
            "[ro.product.model]: [Pixel 7]\n" +
            "[ro.product.device]: [panther]\n" +
            "[ro.build.version.release]: [15]\n" +
            "[ro.build.version.sdk]: [35]\n" +
            "[ro.build.version.security_patch]: [2026-11-05]\n" +
            "[ro.build.version.incremental]: [12345678]\n" +
            "[ro.build.fingerprint]: [google/panther/panther:15/AP4A/12345678:user/release-keys]\n";

        IReadOnlyDictionary<string, string> properties = AdbParsers.ParseGetProp(props);
        var device = new AdbDevice(
            "R58M123456", AdbDeviceState.Device, "device", "Pixel 7",
            "panther", "panther", "3", AdbConnectionKind.Usb);

        AdbDeviceInfo info = AdbParsers.BuildInfo(device, properties);

        Assert.Equal("Google", info.Brand);
        Assert.Equal("Pixel 7", info.Model);
        Assert.Equal("15", info.AndroidVersion);
        Assert.Equal("35", info.SdkLevel);
        Assert.Equal("2026-11-05", info.SecurityPatch);
        Assert.Equal("USB", info.Transport);

        const string users =
            "Users:\n" +
            "\tUserInfo{0:Owner:c13} running\n" +
            "\tUserInfo{10:Work profile:1030} running\n";

        IReadOnlyList<AdbUser> parsed = AdbParsers.ParseUsers(users);

        Assert.Equal(2, parsed.Count);
        Assert.True(parsed[0].IsRunning);
        Assert.False(parsed[0].IsWorkProfile);
        Assert.True(parsed[1].IsWorkProfile);
        Assert.Equal(10, parsed[1].Id);
    }

    [Fact]
    public void Command_builder_only_emits_the_typed_a1_surface_and_rejects_bad_serials()
    {
        Assert.Equal(new[] { "start-server" }, AdbCommandLine.Build(AdbCommand.StartServer()));
        Assert.Equal(new[] { "devices", "-l" }, AdbCommandLine.Build(AdbCommand.Devices()));
        Assert.Equal(
            new[] { "-s", "R58M123456", "shell", "getprop" },
            AdbCommandLine.Build(AdbCommand.GetProperties("R58M123456")));
        Assert.Equal(
            new[] { "-s", "192.168.1.7:5555", "shell", "pm", "list", "users" },
            AdbCommandLine.Build(AdbCommand.ListUsers("192.168.1.7:5555")));

        Assert.Throws<ArgumentException>(() =>
            AdbCommandLine.Build(AdbCommand.GetProperties("bad serial")));
        Assert.Throws<ArgumentException>(() =>
            AdbCommandLine.Build(AdbCommand.ListUsers(string.Empty)));
    }

    [Fact]
    public void Package_identity_is_pinned_to_the_official_google_archive()
    {
        Assert.Equal("35.0.2", AdbPackage.Version);
        Assert.Equal(64, AdbPackage.Sha256.Length);
        Assert.StartsWith("https://dl.google.com/android/repository/", AdbPackage.DownloadUrl);
        Assert.Equal(AdbPackage.Sha256, AdbPackage.Sha256.ToLowerInvariant());
    }
}
