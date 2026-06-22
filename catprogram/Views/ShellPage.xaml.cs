using System.Windows.Controls;
using catprogram.ViewModels;

namespace catprogram.Views;

public partial class ShellPage : Page
{
    public ShellPage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}