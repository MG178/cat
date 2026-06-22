using System.Windows.Controls;
using catprogram.ViewModels;

namespace catprogram.Views;

public partial class DashboardPage : Page
{
    public DashboardPage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
