using System.Windows.Controls;
using PCManager.ViewModels;

namespace PCManager.Views;

public partial class DebloatView : UserControl
{
    public DebloatView()
    {
        InitializeComponent();
        IsVisibleChanged += (_, _) =>
        {
            if (DataContext is DebloatViewModel viewModel)
            {
                viewModel.SetPageVisible(IsVisible);
            }
        };
    }
}
