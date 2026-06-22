using System.Windows;
using System.Windows.Input;
using catprogram.ViewModels;
using catprogram.Views;

namespace catprogram;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel();
        DataContext = ViewModel;
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.LoginSucceeded += ShowShellPage;
        ViewModel.LogoutRequested += ShowLoginPage;
        ShowLoginPage();
    }

    private void ShowLoginPage()
    {
        MainFrame.Navigate(new LoginPage(ViewModel));
    }

    private void ShowShellPage()
    {
        MainFrame.Navigate(new ShellPage(ViewModel));
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
