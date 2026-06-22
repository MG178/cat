using System.Windows.Controls;
using catprogram.ViewModels;

namespace catprogram.Views;

public partial class AttendancePage : Page
{
    public AttendancePage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
