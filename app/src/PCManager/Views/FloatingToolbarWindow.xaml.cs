using System.Windows;
using System.Windows.Input;
using PCManager.Services;
using PCManager.ViewModels;

namespace PCManager.Views;

/// <summary>
/// Small always-on-top pill. Closing it hides the window (the host recreates nothing), and
/// the position is kept for the rest of the session.
/// </summary>
public partial class FloatingToolbarWindow : Window
{
    public FloatingToolbarWindow(FloatingToolbarViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        AppIdentity.ApplyWindowIcon(this);

        // Start near the top-right corner of the working area.
        Rect work = SystemParameters.WorkArea;
        Left = work.Right - 430;
        Top = work.Top + 24;
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
}
