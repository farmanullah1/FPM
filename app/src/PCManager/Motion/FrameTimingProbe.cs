using System.Diagnostics;
using System.Windows.Media;

namespace PCManager.Motion;

/// <summary>
/// Opt-in frame pacing measurement for the U4 budget ("no frame above 16 ms").
/// Enabled only when <c>PCMANAGER_FRAME_TRACE=1</c>; the shell logs the worst frame interval
/// after each page transition, so normal use pays nothing.
/// </summary>
internal sealed class FrameTimingProbe
{
    private static readonly bool Enabled =
        string.Equals(Environment.GetEnvironmentVariable("PCMANAGER_FRAME_TRACE"), "1", StringComparison.Ordinal);

    private readonly Stopwatch _clock = new();
    private TimeSpan _previous;
    private double _worstMilliseconds;
    private int _frames;
    private bool _running;

    public void Start()
    {
        if (!Enabled || _running)
        {
            return;
        }

        _running = true;
        _frames = 0;
        _worstMilliseconds = 0;
        _previous = TimeSpan.Zero;
        _clock.Restart();

        CompositionTarget.Rendering += OnRendering;
    }

    /// <summary>Stops the probe and returns "frames / worst interval"; null when not measuring.</summary>
    public string? Stop()
    {
        if (!_running)
        {
            return null;
        }

        CompositionTarget.Rendering -= OnRendering;
        _running = false;

        return $"Motion frame trace: {_frames} frames, worst frame interval " +
               $"{_worstMilliseconds:0.0} ms, window {_clock.Elapsed.TotalMilliseconds:0} ms.";
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        TimeSpan now = _clock.Elapsed;

        if (_frames > 0)
        {
            _worstMilliseconds = Math.Max(_worstMilliseconds, (now - _previous).TotalMilliseconds);
        }

        _previous = now;
        _frames++;
    }
}
