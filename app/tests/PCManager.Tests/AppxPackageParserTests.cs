using PCManager.Core.Models;
using Xunit;

namespace PCManager.Tests;

public sealed class AppxPackageParserTests
{
    [Fact]
    public void Parse_keeps_user_store_packages_and_filters_protected_ones()
    {
        const string json = """
            [
              {
                "Name": "Contoso.Notes",
                "PackageFullName": "Contoso.Notes_1.2.3.0_x64__abc123",
                "Version": "1.2.3.0",
                "Publisher": "Contoso",
                "InstallLocation": "C:\\Program Files\\WindowsApps\\Contoso.Notes",
                "SignatureKind": "Store",
                "IsFramework": false,
                "IsResourcePackage": false
              },
              {
                "Name": "Microsoft.VCLibs",
                "PackageFullName": "Microsoft.VCLibs_14.0.0.0_x64__abc123",
                "Version": "14.0.0.0",
                "Publisher": "Microsoft",
                "SignatureKind": "Store",
                "IsFramework": true,
                "IsResourcePackage": false
              },
              {
                "Name": "Contoso.Notes.Resources",
                "PackageFullName": "Contoso.Notes.Resources_1.2.3.0_x64__abc123",
                "Version": "1.2.3.0",
                "Publisher": "Contoso",
                "SignatureKind": "Store",
                "IsFramework": false,
                "IsResourcePackage": true
              },
              {
                "Name": "Microsoft.WindowsCalculator",
                "PackageFullName": "Microsoft.WindowsCalculator_11.0.0.0_x64__abc123",
                "Version": "11.0.0.0",
                "Publisher": "Microsoft",
                "SignatureKind": 4,
                "IsFramework": false,
                "IsResourcePackage": false
              },
              {
                "Name": "Microsoft.MicrosoftPCManager",
                "PackageFullName": "Microsoft.MicrosoftPCManager_3.23.3.0_x64__8wekyb3d8bbwe",
                "Version": "3.23.3.0",
                "Publisher": "Microsoft",
                "SignatureKind": "Store",
                "IsFramework": false,
                "IsResourcePackage": false
              }
            ]
            """;

        IReadOnlyList<AppxPackage> packages = AppxPackageParser.Parse(json);

        AppxPackage package = Assert.Single(packages);
        Assert.Equal("Contoso.Notes", package.Name);
        Assert.Equal("Contoso", package.Publisher);
        Assert.Equal("Contoso · v1.2.3.0", package.Subtitle);
        Assert.True(package.CanUninstall);
    }

    [Fact]
    public void Parse_accepts_a_single_object()
    {
        const string json = """
            {
              "Name": "Contoso.Single",
              "PackageFullName": "Contoso.Single_1.0.0.0_x64__abc123",
              "Version": "1.0.0.0",
              "Publisher": "Contoso",
              "SignatureKind": "Store"
            }
            """;

        AppxPackage package = Assert.Single(AppxPackageParser.Parse(json));
        Assert.Equal("Contoso.Single", package.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[{\"Name\":\"broken\"")]
    public void Parse_returns_empty_for_missing_or_malformed_output(string json)
        => Assert.Empty(AppxPackageParser.Parse(json));

    [Fact]
    public void MicrosoftPcManager_is_never_uninstallable()
    {
        var package = new AppxPackage
        {
            Name = "Microsoft.MicrosoftPCManager",
            PackageFullName = "Microsoft.MicrosoftPCManager_3.23.3.0_x64__8wekyb3d8bbwe",
            SignatureKind = "Store"
        };

        Assert.False(package.CanUninstall);
        Assert.True(package.IsMicrosoftPcManager);
    }
}
