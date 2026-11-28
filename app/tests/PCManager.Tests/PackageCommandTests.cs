using PCManager.Core.Android;
using Xunit;

namespace PCManager.Tests;

public sealed class PackageCommandTests
{
    [Fact]
    public void Package_queries_are_typed_and_use_the_requested_user()
    {
        Assert.Equal(
            new[] { "-s", "SERIAL", "shell", "pm", "list", "packages", "-u", "--user", "0" },
            AdbCommandLine.Build(AdbCommand.ListPackages("SERIAL", 0, PackageListFilter.All)));
        Assert.Equal(
            new[] { "-s", "SERIAL", "shell", "pm", "list", "packages", "-s", "-u", "--user", "10" },
            AdbCommandLine.Build(AdbCommand.ListPackages("SERIAL", 10, PackageListFilter.System)));
        Assert.Equal(
            new[] { "-s", "SERIAL", "shell", "pm", "list", "packages", "-e", "--user", "0" },
            AdbCommandLine.Build(AdbCommand.ListPackages("SERIAL", 0, PackageListFilter.Enabled)));
        Assert.Equal(
            new[] { "-s", "SERIAL", "shell", "pm", "list", "packages", "-d", "--user", "0" },
            AdbCommandLine.Build(AdbCommand.ListPackages("SERIAL", 0, PackageListFilter.Disabled)));
    }

    [Fact]
    public void Actions_use_only_the_documented_package_arguments()
    {
        Assert.Equal(
            new[] { "-s", "SERIAL", "shell", "pm", "uninstall", "-k", "--user", "0", "com.example.app" },
            AdbCommandLine.Build(AdbCommand.UninstallPackage("SERIAL", "com.example.app", 0)));
        Assert.Equal(
            new[] { "-s", "SERIAL", "shell", "pm", "disable-user", "--user", "0", "com.example.app" },
            AdbCommandLine.Build(AdbCommand.DisablePackage("SERIAL", "com.example.app", 0)));
        Assert.Equal(
            new[] { "-s", "SERIAL", "shell", "pm", "enable", "--user", "0", "com.example.app" },
            AdbCommandLine.Build(AdbCommand.EnablePackage("SERIAL", "com.example.app", 0)));
        Assert.Equal(
            new[]
            {
                "-s", "SERIAL", "shell", "cmd", "package", "install-existing",
                "--user", "0", "com.example.app",
            },
            AdbCommandLine.Build(AdbCommand.RestorePackage("SERIAL", "com.example.app", 0)));
    }

    [Theory]
    [InlineData("com.example.app;rm -rf /")]
    [InlineData("com.example.app\nrm")]
    [InlineData("com.example..app")]
    [InlineData("com")]
    [InlineData("android")]
    [InlineData("")]
    public void Malformed_package_ids_and_shell_text_are_rejected(string packageId)
    {
        Assert.False(AdbCommandLine.IsValidPackageId(packageId));
        Assert.Throws<ArgumentException>(() =>
            AdbCommandLine.Build(AdbCommand.UninstallPackage("SERIAL", packageId, 0)));
    }

    [Fact]
    public void Negative_user_ids_and_bad_serials_are_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AdbCommandLine.Build(AdbCommand.ListPackages("SERIAL", -1, PackageListFilter.All)));
        Assert.Throws<ArgumentException>(() =>
            AdbCommandLine.Build(AdbCommand.DisablePackage("bad serial", "com.example.app", 0)));
    }

    [Fact]
    public void Package_list_parser_ignores_headers_and_deduplicates()
    {
        const string output =
            "package:com.example.app\n" +
            "package:com.example.app\n" +
            "package:android\n" +
            "not a package\n";

        Assert.Equal(
            new[] { "com.example.app", "android" },
            AdbParsers.ParsePackageList(output));
    }
}
