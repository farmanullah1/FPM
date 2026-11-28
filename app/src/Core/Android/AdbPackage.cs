namespace PCManager.Core.Android;

/// <summary>
/// The one platform-tools package FPM may offer. Version, URL and SHA-256 are fixed here;
/// the package is never bundled, and the UI/service requires consent before downloading.
/// </summary>
public static class AdbPackage
{
    public const string Version = "35.0.2";
    public const string ArchiveName = "platform-tools_r35.0.2-win.zip";
    public const string DownloadUrl =
        "https://dl.google.com/android/repository/platform-tools_r35.0.2-win.zip";
    public const string Sha256 =
        "2975a3eac0b19182748d64195375ad056986561d994fffbdc64332a516300bb9";
    public const long ArchiveBytes = 6_700_829;
    public const string LicenseUrl = "https://developer.android.com/studio/terms";

    public const string LicenseNotice =
        "Google Android SDK Platform-Tools 35.0.2 is governed by the Android SDK License Agreement. " +
        "FPM does not bundle these tools. After you accept, FPM downloads the official archive, " +
        "checks the pinned SHA-256 and requires a valid Google Authenticode signature before use. " +
        "License: developer.android.com/studio/terms";
}
