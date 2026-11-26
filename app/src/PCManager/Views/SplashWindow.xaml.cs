using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using PCManager.Core;
using MotionKit = PCManager.Motion.Motion;

namespace PCManager.Views;

/// <summary>
/// U5 splash: borderless, centred, real startup stages (set by <see cref="SetStage"/>),
/// minimum 600 ms / maximum 3 s enforced by the caller, click or Esc skips.
///
/// Full motion assembles the four brand petals, sweeps light across the emblem and fades the
/// wordmark up. Reduced motion shows the emblem and fades the text; no motion means the final
/// frame immediately. Nothing here blocks input: the window is small, topmost and skippable.
/// </summary>
public partial class SplashWindow : Window
{
    private bool _closing;

    public SplashWindow(bool firstRun)
    {
        InitializeComponent();

        VersionText.Text = $"Version {AppInfo.Version}";
        WelcomeBadge.Visibility = firstRun ? Visibility.Visible : Visibility.Collapsed;

        if (firstRun)
        {
            StatusText.Text = "Welcome — setting things up.";
        }

        Loaded += (_, _) => PlayIntro();
        MouseLeftButtonDown += (_, _) => Skip();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>True when the user dismissed the splash with a click or Esc.</summary>
    public bool WasSkipped { get; private set; }

    /// <summary>Updates the real startup stage label and the gold progress line.</summary>
    public void SetStage(string status, double percent)
    {
        if (_closing)
        {
            return;
        }

        StatusText.Text = status;
        MotionKit.ProgressTween(SplashProgress, Math.Clamp(percent, 0d, 100d));
    }

    /// <summary>Fades and scales out, then closes; instant when motion is off.</summary>
    public async Task CloseAnimatedAsync()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;

        if (!MotionKit.Policy.AllowsFades)
        {
            Close();
            return;
        }

        var finished = new TaskCompletionSource();

        var fade = new DoubleAnimation(1d, 0d, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) => finished.TrySetResult();

        if (MotionKit.Policy.AllowsTransforms)
        {
            var grow = new DoubleAnimation(1d, 1.03d, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Root.RenderTransformOrigin = new Point(0.5, 0.5);
            Root.RenderTransform = new System.Windows.Media.ScaleTransform();
            ((System.Windows.Media.ScaleTransform)Root.RenderTransform)
                .BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, grow);
            ((System.Windows.Media.ScaleTransform)Root.RenderTransform)
                .BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, grow);
        }

        BeginAnimation(OpacityProperty, fade);
        await finished.Task;
        Close();
    }

    private void Skip()
    {
        if (_closing)
        {
            return;
        }

        WasSkipped = true;
        _closing = true;
        Close();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        Skip();
    }

    private void PlayIntro()
    {
        if (MotionKit.Policy.AllowsTransforms)
        {
            AssemblePetals();
            SweepAcrossEmblem();
        }
        else
        {
            ShowAssembledEmblem();
        }

        MotionKit.FadeIn(AppName, TimeSpan.FromMilliseconds(300), delay: TimeSpan.FromMilliseconds(420));
        MotionKit.SlideIn(TitleShift, fromX: 0, fromY: 8, TimeSpan.FromMilliseconds(300),
            delay: TimeSpan.FromMilliseconds(420));
        MotionKit.FadeIn(VersionText, TimeSpan.FromMilliseconds(300), delay: TimeSpan.FromMilliseconds(560));
        MotionKit.FadeIn(StatusText, TimeSpan.FromMilliseconds(300), delay: TimeSpan.FromMilliseconds(560));

        if (WelcomeBadge.Visibility == Visibility.Visible)
        {
            MotionKit.FadeIn(WelcomeBadge, TimeSpan.FromMilliseconds(300), delay: TimeSpan.FromMilliseconds(620));
        }
    }

    private void AssemblePetals()
    {
        MotionKit.ScaleIn(EmblemScale, from: 0.9, TimeSpan.FromMilliseconds(520));

        MotionKit.SlideIn(TopShift, 0, -44, TimeSpan.FromMilliseconds(520));
        MotionKit.SlideIn(RightShift, 44, 0, TimeSpan.FromMilliseconds(520), TimeSpan.FromMilliseconds(70));
        MotionKit.SlideIn(BottomShift, 0, 44, TimeSpan.FromMilliseconds(520), TimeSpan.FromMilliseconds(140));
        MotionKit.SlideIn(LeftShift, -44, 0, TimeSpan.FromMilliseconds(520), TimeSpan.FromMilliseconds(210));

        MotionKit.Fade(PetalTop, 0, 1, TimeSpan.FromMilliseconds(260));
        MotionKit.Fade(PetalRight, 0, 1, TimeSpan.FromMilliseconds(260), TimeSpan.FromMilliseconds(70));
        MotionKit.Fade(PetalBottom, 0, 1, TimeSpan.FromMilliseconds(260), TimeSpan.FromMilliseconds(140));
        MotionKit.Fade(PetalLeft, 0, 1, TimeSpan.FromMilliseconds(260), TimeSpan.FromMilliseconds(210));
        MotionKit.Fade(PetalCore, 0, 1, TimeSpan.FromMilliseconds(240), TimeSpan.FromMilliseconds(260));
    }

    private void SweepAcrossEmblem()
    {
        MotionKit.MoveTo(SweepShift, toX: 150, toY: 0, TimeSpan.FromMilliseconds(520),
            delay: TimeSpan.FromMilliseconds(560));

        var glow = new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromMilliseconds(560) };
        glow.KeyFrames.Add(new LinearDoubleKeyFrame(0d, TimeSpan.Zero));
        glow.KeyFrames.Add(new LinearDoubleKeyFrame(0.85d, TimeSpan.FromMilliseconds(200)));
        glow.KeyFrames.Add(new LinearDoubleKeyFrame(0d, TimeSpan.FromMilliseconds(520)));
        Sweep.BeginAnimation(UIElement.OpacityProperty, glow);
    }

    private void ShowAssembledEmblem()
    {
        Sweep.Visibility = Visibility.Collapsed;

        foreach ((UIElement petal, System.Windows.Media.TranslateTransform shift) in new (UIElement, System.Windows.Media.TranslateTransform)[]
                 {
                     (PetalTop, TopShift), (PetalRight, RightShift),
                     (PetalBottom, BottomShift), (PetalLeft, LeftShift)
                 })
        {
            shift.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
            shift.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
            shift.X = 0;
            shift.Y = 0;
            petal.BeginAnimation(UIElement.OpacityProperty, null);
            petal.Opacity = 1;
        }

        PetalCore.BeginAnimation(UIElement.OpacityProperty, null);
        PetalCore.Opacity = 1;
    }
}
