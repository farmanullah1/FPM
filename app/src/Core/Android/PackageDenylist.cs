namespace PCManager.Core.Android;

/// <summary>
/// Critical packages that can brick a phone or remove the shell/telephony path. The UAD
/// <c>Unsafe</c> tier is also treated as protected by <see cref="AndroidPackage.IsDenylisted"/>.
/// A4 may offer Expert mode only with an exact typed package-name confirmation.
/// </summary>
public static class PackageDenylist
{
    private static readonly HashSet<string> Critical = new(StringComparer.Ordinal)
    {
        "android",
        "com.android.systemui",
        "com.android.phone",
        "com.android.server.telecom",
        "com.android.providers.settings",
        "com.android.providers.telephony",
        "com.android.settings",
        "com.android.shell",
        "com.android.packageinstaller",
        "com.google.android.packageinstaller",
        "com.android.permissioncontroller",
        "com.google.android.permissioncontroller",
        "com.android.launcher",
        "com.android.launcher3",
        "com.google.android.apps.nexuslauncher",
        "com.android.setupwizard",
        "com.google.android.setupwizard",
        "com.android.provision",
        "com.android.certinstaller",
        "com.android.keychain",
        "com.android.location.fused",
        "com.android.mms.service",
        "com.android.phone.a_overlay",
        "com.android.server.telecom.a_overlay",
        "com.sec.android.app.launcher",
        "com.miui.home",
        "com.huawei.android.launcher",
        "com.oneplus.launcher",
        "com.coloros.launcher",
        "com.oppo.launcher",
        "com.vivo.launcher",
        "com.transsion.XOSLauncher",
        "com.tcl.launcher",
        "com.motorola.launcher3",
        "com.sonymobile.home",
        "com.samsung.android.SettingsReceiver",
        "com.miui.securitycenter",
        "com.miui.securitycore",
        "com.miui.securityadd",
        "com.miui.core",
        "com.miui.rom",
        "com.xiaomi.misettings",
        "com.coloros.securitypermission",
        "com.samsung.android.kgclient",
        "com.samsung.android.storage.watchstoragemanager",
        "com.motorola.android.providers.settings",
        "com.android.updater",
    };

    public static bool Contains(string packageId) => Critical.Contains(packageId);

    public const string Message =
        "Protected system package. It cannot be uninstalled or disabled without Expert mode " +
        "and typing the exact package name; removing it can cause a bootloop or factory reset.";
}
