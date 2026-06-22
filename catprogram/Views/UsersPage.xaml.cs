using System.Windows.Controls;
using catprogram.ViewModels;

namespace catprogram.Views;

public partial class UsersPage : Page
{
    public UsersPage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
