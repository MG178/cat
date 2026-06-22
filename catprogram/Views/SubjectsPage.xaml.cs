using System.Windows.Controls;
using catprogram.ViewModels;

namespace catprogram.Views;

public partial class SubjectsPage : Page
{
    public SubjectsPage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
