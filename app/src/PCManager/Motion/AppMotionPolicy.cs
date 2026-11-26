using System.Runtime.InteropServices;
using System.Windows;
using PCManager.Core;
using PCManager.Services.Abstractions;

namespace PCManager.Motion;

/// <summary>
/// Reads the U4 environment on every query, so toggling Windows animations, switching to
/// battery saver or changing the app setting applies without a restart.
/// </summary>
public sealed class AppMotionPolicy(ISettingsService settings) : IMotionPolicy
{
    public MotionLevel Level => Resolve(windowVisible: true);

    public bool AllowsFades => MotionPolicy.AllowsFades(Level);

    public bool AllowsTransforms => MotionPolicy.AllowsTransforms(Level);

    public MotionLevel Resolve(bool windowVisible)
    {
        MotionEnvironment environment = new(
            SystemAnimationsEnabled: SystemParameters.ClientAreaAnimation,
            ReduceMotion: settings.Current.ReduceMotion,
            BatterySaver: BatterySaverIsOn(),
            RemoteSession: IsRemoteSession(),
            LowResourceMode: settings.Current.LowResourceModeEnabled,
            WindowVisible: windowVisible);

        return MotionPolicy.Resolve(environment);
    }

    private static bool IsRemoteSession()
    {
        try
        {
            return SystemParameters.IsRemoteSession;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>GetSystemPowerStatus exposes the Windows "battery saver" flag as SystemStatusFlag.</summary>
    private static bool BatterySaverIsOn()
    {
        try
        {
            return GetSystemPowerStatus(out SystemPowerStatus status) && status.SystemStatusFlag == 1;
        }
        catch (Exception)
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
}
