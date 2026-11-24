using System.Windows;
using System.Windows.Controls;

namespace PCManager.Views;

public partial class HomeView : UserControl
{
    /// <summary>Content-area width (DIP) at which the Bento two-column arrangement replaces the compact stack.</summary>
    private const double WideBreakpoint = 760;

    public HomeView()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyLayout(ActualWidth);
        SizeChanged += (_, e) => ApplyLayout(e.NewSize.Width);
    }

    private void ApplyLayout(double width)
    {
        bool wide = width >= WideBreakpoint;

        if (wide)
        {
            Place(TipCard, row: 0, column: 0, columnSpan: 3);
            Place(BoostCard, row: 1, column: 0, columnSpan: 1);
            Place(UtilityGrid, row: 1, column: 2, columnSpan: 1);
            Place(CleanupCard, row: 2, column: 0, columnSpan: 1);
            Place(CaptureCard, row: 2, column: 2, columnSpan: 1);
        }
        else
        {
            Place(TipCard, row: 0, column: 0, columnSpan: 3);
            Place(BoostCard, row: 1, column: 0, columnSpan: 3);
            Place(UtilityGrid, row: 2, column: 0, columnSpan: 3);
            Place(CleanupCard, row: 3, column: 0, columnSpan: 3);
            Place(CaptureCard, row: 4, column: 0, columnSpan: 3);
        }
    }

    private static void Place(UIElement element, int row, int column, int columnSpan)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetColumnSpan(element, columnSpan);
    }
}
