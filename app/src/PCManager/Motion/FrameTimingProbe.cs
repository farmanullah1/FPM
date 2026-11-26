using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCManager.Motion;

/// <summary>
/// Opt-in frame pacing measurement for the U4 budget ("no frame above 16 ms").
/// Enabled only when <c>PCMANAGER_FRAME_TRACE=1</c>; it samples for the given window, then
/// reports the frame count and worst interval, so normal use pays nothing.
/// </summary>
internal sealed class FrameTimingProbe
{
    private const double BudgetMilliseconds = 16d;

    private static readonly bool Enabled =
        string.Equals(Environment.GetEnvironmentVariable("PCMANAGER_FRAME_TRACE"), "1", StringComparison.Ordinal);

    private readonly Stopwatch _clock = new();
    private TimeSpan _previous;
    private double _worstMilliseconds;
    private int _frames;
    private DispatcherTimer? _window;

    public void Measure(TimeSpan duration, Action<string>? report = null)
    {
        if (!Enabled)
        {
            return;
        }

        _clock.Restart();
        _frames = 0;
        _worstMilliseconds = 0;
        _previous = TimeSpan.Zero;

        CompositionTarget.Rendering += OnRendering;

        _window = new DispatcherTimer(DispatcherPriority.Render) { Interval = duration };
        _window.Tick += (_, _) =>
        {
            _window?.Stop();
            _window = null;
            CompositionTarget.Rendering -= OnRendering;

            string verdict = _worstMilliseconds <= BudgetMilliseconds ? "within" : "over";
            report?.Invoke(
                $"Motion frame trace: {_frames} frames in {_clock.Elapsed.TotalMilliseconds:0} ms, " +
                $"worst frame {_worstMilliseconds:0.0} ms ({verdict} the 16 ms budget).");
        };
        _window.Start();
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
