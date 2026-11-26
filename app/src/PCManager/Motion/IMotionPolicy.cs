using PCManager.Core;

namespace PCManager.Motion;

/// <summary>
/// One authority for "may this animate right now" (plan U4). Views and controls never read
/// system parameters directly; they call this and get Full / Reduced / None.
/// </summary>
public interface IMotionPolicy
{
    /// <summary>Current level, resolved for a visible window.</summary>
    MotionLevel Level { get; }

    /// <summary>Resolve again, taking the window's visibility into account.</summary>
    MotionLevel Resolve(bool windowVisible);

    bool AllowsFades { get; }

    bool AllowsTransforms { get; }
}
