using System.Windows.Controls;
using catprogram.ViewModels;

namespace catprogram.Views;

public partial class QrPage : Page
{
    public QrPage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
