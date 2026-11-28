using PCManager.Core.Android;
using PCManager.Services.Android;
using Xunit;

namespace PCManager.Tests;

public sealed class AdbServiceTests
{
    [Fact]
    public async Task No_device_is_a_typed_state_and_fpm_started_server_is_stopped_on_shutdown()
    {
        using var temp = new TempFiles();
        FakeRunner runner = new(
            new AdbProcessResult(0, "daemon not running; starting now at tcp:5037\ndaemon started successfully\n", ""),
            new AdbProcessResult(0, "List of devices attached\n\n", ""));
        AdbService service = CreateService(temp, runner);

        AdbSnapshot snapshot = await service.RefreshAsync();

        Assert.Equal(AdbStatusKind.NoDevice, snapshot.Kind);
        Assert.True(snapshot.ServerStartedByFpm);
        Assert.Equal(new[] { AdbCommandKind.StartServer, AdbCommandKind.Devices }, runner.Kinds);

        service.Shutdown();

        Assert.Equal(AdbCommandKind.KillServer, runner.Kinds[^1]);
    }

    [Fact]
    public async Task Existing_server_is_not_stopped_because_fpm_did_not_start_it()
    {
        using var temp = new TempFiles();
        FakeRunner runner = new(
            new AdbProcessResult(0, "", ""),
            new AdbProcessResult(0, "List of devices attached\n" + DeviceLine(), ""));
        AdbService service = CreateService(temp, runner);

        await service.RefreshAsync();
        service.Shutdown();

        Assert.DoesNotContain(AdbCommandKind.KillServer, runner.Kinds);
    }

    [Theory]
    [InlineData("R58M123456 unauthorized usb:1-2 transport_id:4\n", AdbStatusKind.Unauthorized)]
    [InlineData("R58M123456 offline transport_id:4\n", AdbStatusKind.Offline)]
    [InlineData("???????????? no permissions (user in plugdev group)\n", AdbStatusKind.NoPermissions)]
    public async Task Device_problem_states_are_reported_instead_of_being_called_ready(
        string line,
        AdbStatusKind expected)
    {
        using var temp = new TempFiles();
        FakeRunner runner = new(
            new AdbProcessResult(0, "", ""),
            new AdbProcessResult(0, "List of devices attached\n" + line, ""));
        AdbService service = CreateService(temp, runner);

        AdbSnapshot snapshot = await service.RefreshAsync();

        Assert.Equal(expected, snapshot.Kind);
        Assert.False(snapshot.HasUsableDevice);
    }

    [Fact]
    public async Task Failed_devices_command_is_a_typed_process_failure()
    {
        using var temp = new TempFiles();
        FakeRunner runner = new(
            new AdbProcessResult(0, "", ""),
            new AdbProcessResult(7, "", "adb: device offline"));
        AdbService service = CreateService(temp, runner);

        AdbSnapshot snapshot = await service.RefreshAsync();

        Assert.Equal(AdbStatusKind.ProcessFailed, snapshot.Kind);
        Assert.Contains("device offline", snapshot.Message);
    }

    [Fact]
    public async Task Authorized_device_reads_getprop_and_profiles_with_the_owned_server()
    {
        using var temp = new TempFiles();
        FakeRunner runner = new(
            new AdbProcessResult(0, "daemon started successfully\n", ""),
            new AdbProcessResult(0, "List of devices attached\n" + DeviceLine(), ""),
            new AdbProcessResult(0, "[ro.product.model]: [Pixel 7]\n[ro.build.version.release]: [15]\n", ""),
            new AdbProcessResult(0, "UserInfo{0:Owner:c13} running\n", ""));
        AdbService service = CreateService(temp, runner);

        AdbSnapshot snapshot = await service.RefreshAsync();
        AdbDevice device = Assert.Single(snapshot.Devices);
        AdbDeviceInfo? info = await service.GetDeviceInfoAsync(device);
        IReadOnlyList<AdbUser> users = await service.GetUsersAsync(device);

        Assert.Equal("Pixel 7", info!.Model);
        Assert.Equal("15", info.AndroidVersion);
        Assert.Single(users);
        Assert.Equal(
            new[] { AdbCommandKind.StartServer, AdbCommandKind.Devices, AdbCommandKind.GetProperties, AdbCommandKind.ListUsers },
            runner.Kinds);
        Assert.All(runner.Calls.Skip(2), call => Assert.Equal(device.Serial, call.Command.Serial));
    }

    [Fact]
    public async Task Declining_the_download_never_invokes_the_package_source()
    {
        using var temp = new TempFiles();
        var provisioner = new FakeProvisioner();
        AdbService service = CreateService(temp, new FakeRunner(), provisioner: provisioner);

        AdbSnapshot snapshot = await service.InstallPlatformToolsAsync(consented: false);

        Assert.Equal(AdbStatusKind.Declined, snapshot.Kind);
        Assert.False(provisioner.Called);
    }

    [Fact]
    public async Task Locator_prefers_a_configured_signed_path_and_does_not_accept_an_unsigned_one()
    {
        using var temp = new TempFiles();
        string configured = temp.CreateAdb("configured");
        string local = temp.CreateAdb("local");
        var settings = new FakeSettings { Current = { AdbPath = configured } };
        var signature = new FakeSignature(configured: false, local: true);
        AdbService service = new(
            settings,
            new FakeRunner(),
            signature,
            new FakeProvisioner(),
            new FakeLogger(),
            new AdbSearchPaths(temp.LocalPlatformTools, Array.Empty<string>(), Array.Empty<string>()));

        AdbLocateResult result = await service.LocateAsync();

        Assert.Equal(AdbStatusKind.Ready, result.Kind);
        Assert.Equal(local, result.ExecutablePath);
        Assert.True(signature.Checked(configured));
        Assert.True(signature.Checked(local));
    }

    [Fact]
    public async Task Missing_adb_and_unsigned_adb_are_distinct_states()
    {
        using var temp = new TempFiles(createLocalAdb: false);
        var settings = new FakeSettings();
        var missing = new AdbService(
            settings, new FakeRunner(), new FakeSignature(), new FakeProvisioner(), new FakeLogger(),
            new AdbSearchPaths(temp.LocalPlatformTools, Array.Empty<string>(), Array.Empty<string>()));

        Assert.Equal(AdbStatusKind.Missing, (await missing.LocateAsync()).Kind);

        string local = temp.CreateAdb("local");
        var unsigned = new AdbService(
            settings, new FakeRunner(), new FakeSignature(local: false), new FakeProvisioner(), new FakeLogger(),
            new AdbSearchPaths(temp.LocalPlatformTools, Array.Empty<string>(), Array.Empty<string>()));

        Assert.Equal(AdbStatusKind.Unverified, (await unsigned.LocateAsync()).Kind);
    }

    private static AdbService CreateService(
        TempFiles temp,
        FakeRunner runner,
        FakeProvisioner? provisioner = null) =>
        new(
            new FakeSettings(),
            runner,
            new FakeSignature(local: true),
            provisioner ?? new FakeProvisioner(),
            new FakeLogger(),
            new AdbSearchPaths(temp.LocalPlatformTools, Array.Empty<string>(), Array.Empty<string>()));

    private static string DeviceLine() =>
        "R58M123456 device product:panther model:Pixel_7 device:panther transport_id:3\n";
}
