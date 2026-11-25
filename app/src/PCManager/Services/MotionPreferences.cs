using System.Windows;
using PCManager.Core;

namespace PCManager.Services;

/// <summary>
/// Windows exposes "Animation effects" as the client-area animation system parameter.
/// It is read on every use (not cached) so toggling the setting applies without a restart.
/// </summary>
internal static class MotionPreferences
{
    public static bool AnimationsEnabled => MotionPolicy.ShouldAnimate(SystemParameters.ClientAreaAnimation);
}
