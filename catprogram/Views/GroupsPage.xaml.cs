using System.Windows.Controls;
using catprogram.ViewModels;

namespace catprogram.Views;

public partial class GroupsPage : Page
{
    public GroupsPage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
