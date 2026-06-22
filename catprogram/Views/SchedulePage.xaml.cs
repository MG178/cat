using System.Windows.Controls;
using catprogram.ViewModels;

namespace catprogram.Views;

public partial class SchedulePage : Page
{
    public SchedulePage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
