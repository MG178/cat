using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using catprogram.Models;
using catprogram.ViewModels;

namespace catprogram;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void LoginPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.LoginPassword = LoginPasswordBox.Password;
    }

    private void NewUserPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.NewUserPassword = NewUserPasswordBox.Password;
    }

    private void UsersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if ((sender as DataGrid)?.SelectedItem is UserItem user)
        {
            ViewModel.SelectedUserId = user.Id;
            ViewModel.NewUserName = user.FullName;
            ViewModel.NewUserEmail = user.Email;
            ViewModel.NewUserRoleId = user.RoleId;
            ViewModel.NewUserGroupId = user.GroupId;
            NewUserPasswordBox.Password = string.Empty;
        }
    }

    private void GroupsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if ((sender as DataGrid)?.SelectedItem is catprogram.Models.GroupItem group)
        {
            ViewModel.SelectedGroupId = group.Id;
            ViewModel.NewGroupName = group.Name;
            ViewModel.NewGroupCourse = group.Course;
            ViewModel.NewGroupDepartment = group.Department;
        }
    }

    private void SubjectsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if ((sender as DataGrid)?.SelectedItem is SubjectItem subject)
        {
            ViewModel.SelectedSubjectId = subject.Id;
            ViewModel.NewSubjectName = subject.Name;
            ViewModel.NewSubjectTeacherId = subject.TeacherId;
        }
    }

    private void SchedulesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if ((sender as DataGrid)?.SelectedItem is ScheduleItem schedule)
        {
            ViewModel.SelectedScheduleId = schedule.Id;
            ViewModel.NewScheduleGroupId = schedule.GroupId;
            ViewModel.NewScheduleSubjectId = schedule.SubjectId;
            ViewModel.NewScheduleTeacherId = schedule.TeacherId;
            ViewModel.NewRoom = schedule.Room;
            ViewModel.NewStartTime = schedule.StartTime;
            ViewModel.NewEndTime = schedule.EndTime;
            ViewModel.NewDay = schedule.DayOfWeek;
            ViewModel.NewLessonType = schedule.LessonType;
        }
    }

    private void QrSessionsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if ((sender as DataGrid)?.SelectedItem is QrSessionItem qrSession)
        {
            ViewModel.SelectedQrSessionId = qrSession.Id;
            ViewModel.LatestQrCode = qrSession.QrCodeData;
        }
    }
}