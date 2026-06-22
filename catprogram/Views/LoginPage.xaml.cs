using System.Windows.Controls;
using catprogram.ViewModels;

namespace catprogram.Views;

public partial class LoginPage : Page
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public LoginPage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void PasswordBoxControl_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        ViewModel.LoginPassword = PasswordBoxControl.Password;
    }
}
