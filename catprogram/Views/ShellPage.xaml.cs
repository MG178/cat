using System.ComponentModel;
using System.Windows.Controls;
using catprogram.Models;
using catprogram.ViewModels;

namespace catprogram.Views;

public partial class ShellPage : Page
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public ShellPage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += ShellPage_Loaded;
        Unloaded += ShellPage_Unloaded;
    }

    private void ShellPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        NavigateSection();
    }

    private void ShellPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentSection))
        {
            NavigateSection();
        }
    }

    private void NavigateSection()
    {
        switch (ViewModel.CurrentSection)
        {
            case AppSection.Dashboard:
                SectionFrame.Navigate(new DashboardPage(ViewModel));
                break;
            case AppSection.Users:
                SectionFrame.Navigate(new UsersPage(ViewModel));
                break;
            case AppSection.Groups:
                SectionFrame.Navigate(new GroupsPage(ViewModel));
                break;
            case AppSection.Subjects:
                SectionFrame.Navigate(new SubjectsPage(ViewModel));
                break;
            case AppSection.Schedule:
                SectionFrame.Navigate(new SchedulePage(ViewModel));
                break;
            case AppSection.Qr:
                SectionFrame.Navigate(new QrPage(ViewModel));
                break;
            case AppSection.Attendance:
                SectionFrame.Navigate(new AttendancePage(ViewModel));
                break;
        }
    }
}
