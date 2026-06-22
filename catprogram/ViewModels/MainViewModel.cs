using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using catprogram.Models;
using catprogram.Services;

namespace catprogram.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly LocalDatabaseService _database = new();

    private bool _isLoggedIn;
    private string _loginEmail = "admin@collegehub.local";
    private string _loginPassword = "Admin123!";
    private string _statusMessage = "Готово к работе.";
    private string _scanQrCode = string.Empty;
    private string _newUserName = string.Empty;
    private string _newUserEmail = string.Empty;
    private string _newUserPassword = string.Empty;
    private string _newGroupName = string.Empty;
    private string _newGroupDepartment = string.Empty;
    private string _newSubjectName = string.Empty;
    private string _newRoom = string.Empty;
    private string _newStartTime = "08:30";
    private string _newEndTime = "10:00";
    private string _newDay = "Monday";
    private string _newLessonType = "lecture";
    private int _newGroupCourse = 1;
    private int _newUserRoleId = 3;
    private int? _newUserGroupId;
    private int? _newSubjectTeacherId = 2;
    private int _newScheduleGroupId = 1;
    private int _newScheduleSubjectId = 1;
    private int? _newScheduleTeacherId = 2;
    private int? _selectedUserId;
    private int? _selectedGroupId;
    private int? _selectedSubjectId;
    private int? _selectedScheduleId;
    private int? _selectedQrSessionId;
    private UserItem? _currentUser;
    private QrSessionItem? _selectedSessionToGenerateFrom;
    private bool _isBusy;
    private string _latestQrCode = string.Empty;
    private string _expiresMinutes = "30";

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        _database.Initialize();

        LoginCommand = new RelayCommand(_ => Login(), _ => !_isLoggedIn);
        LogoutCommand = new RelayCommand(_ => Logout(), _ => _isLoggedIn);
        RefreshCommand = new RelayCommand(_ => RefreshAll());
        CreateUserCommand = new RelayCommand(_ => CreateUser(), _ => _isLoggedIn);
        UpdateUserCommand = new RelayCommand(_ => UpdateUser(), _ => _isLoggedIn && _selectedUserId is not null);
        DeleteUserCommand = new RelayCommand(_ => DeleteUser(), _ => _isLoggedIn && _selectedUserId is not null);
        CreateGroupCommand = new RelayCommand(_ => CreateGroup(), _ => _isLoggedIn);
        UpdateGroupCommand = new RelayCommand(_ => UpdateGroup(), _ => _isLoggedIn && _selectedGroupId is not null);
        DeleteGroupCommand = new RelayCommand(_ => DeleteGroup(), _ => _isLoggedIn && _selectedGroupId is not null);
        CreateSubjectCommand = new RelayCommand(_ => CreateSubject(), _ => _isLoggedIn);
        UpdateSubjectCommand = new RelayCommand(_ => UpdateSubject(), _ => _isLoggedIn && _selectedSubjectId is not null);
        DeleteSubjectCommand = new RelayCommand(_ => DeleteSubject(), _ => _isLoggedIn && _selectedSubjectId is not null);
        CreateScheduleCommand = new RelayCommand(_ => CreateSchedule(), _ => _isLoggedIn);
        UpdateScheduleCommand = new RelayCommand(_ => UpdateSchedule(), _ => _isLoggedIn && _selectedScheduleId is not null);
        DeleteScheduleCommand = new RelayCommand(_ => DeleteSchedule(), _ => _isLoggedIn && _selectedScheduleId is not null);
        GenerateQrCommand = new RelayCommand(_ => GenerateQr(), _ => _isLoggedIn && _selectedScheduleId is not null);
        ToggleQrStateCommand = new RelayCommand(_ => ToggleQrState(), _ => _isLoggedIn && _selectedQrSessionId is not null);
        ScanAttendanceCommand = new RelayCommand(_ => ScanAttendance(), _ => _isLoggedIn);
        SelectQrFromLatestCommand = new RelayCommand(_ => SelectLatestQr());

        RefreshAll();
    }

    public ObservableCollection<RoleItem> Roles { get; private set; } = new();
    public ObservableCollection<GroupItem> Groups { get; private set; } = new();
    public ObservableCollection<UserItem> Users { get; private set; } = new();
    public ObservableCollection<SubjectItem> Subjects { get; private set; } = new();
    public ObservableCollection<ScheduleItem> Schedules { get; private set; } = new();
    public ObservableCollection<QrSessionItem> QrSessions { get; private set; } = new();
    public ObservableCollection<AttendanceItem> Attendance { get; private set; } = new();

    public DashboardSnapshot Dashboard { get; private set; } = new(0, 0, 0, 0, 0, 0, 0, string.Empty);

    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set => SetField(ref _isLoggedIn, value);
    }

    public UserItem? CurrentUser
    {
        get => _currentUser;
        set => SetField(ref _currentUser, value);
    }

    public string LoginEmail
    {
        get => _loginEmail;
        set => SetField(ref _loginEmail, value);
    }

    public string LoginPassword
    {
        get => _loginPassword;
        set => SetField(ref _loginPassword, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string ScanQrCode
    {
        get => _scanQrCode;
        set => SetField(ref _scanQrCode, value);
    }

    public string LatestQrCode
    {
        get => _latestQrCode;
        set => SetField(ref _latestQrCode, value);
    }

    public string ExpiresMinutes
    {
        get => _expiresMinutes;
        set => SetField(ref _expiresMinutes, value);
    }

    public string NewUserName
    {
        get => _newUserName;
        set => SetField(ref _newUserName, value);
    }

    public string NewUserEmail
    {
        get => _newUserEmail;
        set => SetField(ref _newUserEmail, value);
    }

    public string NewUserPassword
    {
        get => _newUserPassword;
        set => SetField(ref _newUserPassword, value);
    }

    public int NewUserRoleId
    {
        get => _newUserRoleId;
        set => SetField(ref _newUserRoleId, value);
    }

    public int? NewUserGroupId
    {
        get => _newUserGroupId;
        set => SetField(ref _newUserGroupId, value);
    }

    public string NewGroupName
    {
        get => _newGroupName;
        set => SetField(ref _newGroupName, value);
    }

    public int NewGroupCourse
    {
        get => _newGroupCourse;
        set => SetField(ref _newGroupCourse, value);
    }

    public string NewGroupDepartment
    {
        get => _newGroupDepartment;
        set => SetField(ref _newGroupDepartment, value);
    }

    public string NewSubjectName
    {
        get => _newSubjectName;
        set => SetField(ref _newSubjectName, value);
    }

    public int? NewSubjectTeacherId
    {
        get => _newSubjectTeacherId;
        set => SetField(ref _newSubjectTeacherId, value);
    }

    public int NewScheduleGroupId
    {
        get => _newScheduleGroupId;
        set => SetField(ref _newScheduleGroupId, value);
    }

    public int NewScheduleSubjectId
    {
        get => _newScheduleSubjectId;
        set => SetField(ref _newScheduleSubjectId, value);
    }

    public int? NewScheduleTeacherId
    {
        get => _newScheduleTeacherId;
        set => SetField(ref _newScheduleTeacherId, value);
    }

    public string NewRoom
    {
        get => _newRoom;
        set => SetField(ref _newRoom, value);
    }

    public string NewStartTime
    {
        get => _newStartTime;
        set => SetField(ref _newStartTime, value);
    }

    public string NewEndTime
    {
        get => _newEndTime;
        set => SetField(ref _newEndTime, value);
    }

    public string NewDay
    {
        get => _newDay;
        set => SetField(ref _newDay, value);
    }

    public string NewLessonType
    {
        get => _newLessonType;
        set => SetField(ref _newLessonType, value);
    }

    public int? SelectedUserId
    {
        get => _selectedUserId;
        set => SetField(ref _selectedUserId, value);
    }

    public int? SelectedGroupId
    {
        get => _selectedGroupId;
        set => SetField(ref _selectedGroupId, value);
    }

    public int? SelectedSubjectId
    {
        get => _selectedSubjectId;
        set => SetField(ref _selectedSubjectId, value);
    }

    public int? SelectedScheduleId
    {
        get => _selectedScheduleId;
        set => SetField(ref _selectedScheduleId, value);
    }

    public int? SelectedQrSessionId
    {
        get => _selectedQrSessionId;
        set => SetField(ref _selectedQrSessionId, value);
    }

    public QrSessionItem? SelectedSessionToGenerateFrom
    {
        get => _selectedSessionToGenerateFrom;
        set => SetField(ref _selectedSessionToGenerateFrom, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public RelayCommand LoginCommand { get; }
    public RelayCommand LogoutCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand CreateUserCommand { get; }
    public RelayCommand UpdateUserCommand { get; }
    public RelayCommand DeleteUserCommand { get; }
    public RelayCommand CreateGroupCommand { get; }
    public RelayCommand UpdateGroupCommand { get; }
    public RelayCommand DeleteGroupCommand { get; }
    public RelayCommand CreateSubjectCommand { get; }
    public RelayCommand UpdateSubjectCommand { get; }
    public RelayCommand DeleteSubjectCommand { get; }
    public RelayCommand CreateScheduleCommand { get; }
    public RelayCommand UpdateScheduleCommand { get; }
    public RelayCommand DeleteScheduleCommand { get; }
    public RelayCommand GenerateQrCommand { get; }
    public RelayCommand ToggleQrStateCommand { get; }
    public RelayCommand ScanAttendanceCommand { get; }
    public RelayCommand SelectQrFromLatestCommand { get; }

    public void RefreshAll()
    {
        Roles = _database.GetRoles();
        Groups = _database.GetGroups();
        Users = _database.GetUsers();
        Subjects = _database.GetSubjects();
        Schedules = _database.GetSchedules();
        QrSessions = _database.GetQrSessions();
        Attendance = _database.GetAttendance();
        Dashboard = _database.GetDashboardSnapshot();
        LatestQrCode = _database.GetLatestQrCode();

        if (_newUserGroupId is null && Groups.Count > 0)
        {
            _newUserGroupId = Groups[0].Id;
        }

        if (CurrentUser is not null)
        {
            SelectedUserId ??= CurrentUser.Id;
        }

        RaiseAllPropertyChanged();
        RaiseCanExecuteChanged();
    }

    private void Login()
    {
        try
        {
            if (_database.TryLogin(LoginEmail, LoginPassword, out UserItem? user) && user is not null)
            {
                CurrentUser = user;
                IsLoggedIn = true;
                StatusMessage = $"Вход выполнен: {user.FullName}.";
                SelectedUserId = user.Id;
                RefreshAll();
                MessageBox.Show($"Добро пожаловать, {user.FullName}!", "CollegeHub", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = "Неверные учетные данные.";
                MessageBox.Show("Проверьте email и пароль.", "CollegeHub", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "CollegeHub", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Logout()
    {
        IsLoggedIn = false;
        CurrentUser = null;
        StatusMessage = "Сеанс завершен.";
        RaiseCanExecuteChanged();
    }

    private void CreateUser()
    {
        try
        {
            _database.CreateUser(NewUserName, NewUserEmail, NewUserPassword, NewUserRoleId, NewUserRoleId == 3 ? NewUserGroupId : null);
            StatusMessage = "Пользователь создан.";
            RefreshAll();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "CollegeHub", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateUser()
    {
        if (SelectedUserId is null)
        {
            return;
        }

        try
        {
            _database.UpdateUser(SelectedUserId.Value, NewUserName, NewUserEmail, string.IsNullOrWhiteSpace(NewUserPassword) ? null : NewUserPassword, NewUserRoleId, NewUserRoleId == 3 ? NewUserGroupId : null);
            StatusMessage = "Пользователь обновлен.";
            RefreshAll();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "CollegeHub", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteUser()
    {
        if (SelectedUserId is null)
        {
            return;
        }

        _database.DeleteUser(SelectedUserId.Value);
        StatusMessage = "Пользователь удален.";
        RefreshAll();
    }

    private void CreateGroup()
    {
        try
        {
            _database.CreateGroup(NewGroupName, NewGroupCourse, NewGroupDepartment);
            StatusMessage = "Группа создана.";
            RefreshAll();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "CollegeHub", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateGroup()
    {
        if (SelectedGroupId is null)
        {
            return;
        }

        _database.UpdateGroup(SelectedGroupId.Value, NewGroupName, NewGroupCourse, NewGroupDepartment);
        StatusMessage = "Группа обновлена.";
        RefreshAll();
    }

    private void DeleteGroup()
    {
        if (SelectedGroupId is null)
        {
            return;
        }

        _database.DeleteGroup(SelectedGroupId.Value);
        StatusMessage = "Группа удалена.";
        RefreshAll();
    }

    private void CreateSubject()
    {
        _database.CreateSubject(NewSubjectName, NewSubjectTeacherId);
        StatusMessage = "Предмет создан.";
        RefreshAll();
    }

    private void UpdateSubject()
    {
        if (SelectedSubjectId is null)
        {
            return;
        }

        _database.UpdateSubject(SelectedSubjectId.Value, NewSubjectName, NewSubjectTeacherId);
        StatusMessage = "Предмет обновлен.";
        RefreshAll();
    }

    private void DeleteSubject()
    {
        if (SelectedSubjectId is null)
        {
            return;
        }

        _database.DeleteSubject(SelectedSubjectId.Value);
        StatusMessage = "Предмет удален.";
        RefreshAll();
    }

    private void CreateSchedule()
    {
        _database.CreateSchedule(NewScheduleGroupId, NewScheduleSubjectId, NewScheduleTeacherId, NewRoom, NewStartTime, NewEndTime, NewDay, NewLessonType);
        StatusMessage = "Занятие добавлено.";
        RefreshAll();
    }

    private void UpdateSchedule()
    {
        if (SelectedScheduleId is null)
        {
            return;
        }

        _database.UpdateSchedule(SelectedScheduleId.Value, NewScheduleGroupId, NewScheduleSubjectId, NewScheduleTeacherId, NewRoom, NewStartTime, NewEndTime, NewDay, NewLessonType);
        StatusMessage = "Занятие обновлено.";
        RefreshAll();
    }

    private void DeleteSchedule()
    {
        if (SelectedScheduleId is null)
        {
            return;
        }

        _database.DeleteSchedule(SelectedScheduleId.Value);
        StatusMessage = "Занятие удалено.";
        RefreshAll();
    }

    private void GenerateQr()
    {
        if (SelectedScheduleId is null)
        {
            return;
        }

        if (!int.TryParse(ExpiresMinutes, out int expiresMinutes))
        {
            expiresMinutes = 30;
        }

        QrSessionItem session = _database.GenerateQrSession(SelectedScheduleId.Value, expiresMinutes);
        LatestQrCode = session.QrCodeData;
        StatusMessage = "QR-сессия сгенерирована.";
        RefreshAll();
    }

    private void ToggleQrState()
    {
        if (SelectedQrSessionId is null)
        {
            return;
        }

        QrSessionItem session = QrSessions.FirstOrDefault(x => x.Id == SelectedQrSessionId.Value)!;
        _database.SetQrSessionState(SelectedQrSessionId.Value, !session.IsActive);
        StatusMessage = "Состояние QR-сессии изменено.";
        RefreshAll();
    }

    private void ScanAttendance()
    {
        if (CurrentUser is null)
        {
            return;
        }

        string qrCode = string.IsNullOrWhiteSpace(ScanQrCode) ? LatestQrCode : ScanQrCode.Trim();
        AttendanceScanResult result = _database.ScanAttendance(qrCode, CurrentUser.Id);
        StatusMessage = result.Message;
        MessageBox.Show(result.Message, "CollegeHub", MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        RefreshAll();
    }

    private void SelectLatestQr()
    {
        if (QrSessions.Count > 0)
        {
            SelectedQrSessionId = QrSessions[0].Id;
        }
    }

    private void RaiseAllPropertyChanged()
    {
        OnPropertyChanged(nameof(Roles));
        OnPropertyChanged(nameof(Groups));
        OnPropertyChanged(nameof(Users));
        OnPropertyChanged(nameof(Subjects));
        OnPropertyChanged(nameof(Schedules));
        OnPropertyChanged(nameof(QrSessions));
        OnPropertyChanged(nameof(Attendance));
        OnPropertyChanged(nameof(Dashboard));
        OnPropertyChanged(nameof(CurrentUser));
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(LatestQrCode));
    }

    private void RaiseCanExecuteChanged()
    {
        LoginCommand.RaiseCanExecuteChanged();
        LogoutCommand.RaiseCanExecuteChanged();
        CreateUserCommand.RaiseCanExecuteChanged();
        UpdateUserCommand.RaiseCanExecuteChanged();
        DeleteUserCommand.RaiseCanExecuteChanged();
        CreateGroupCommand.RaiseCanExecuteChanged();
        UpdateGroupCommand.RaiseCanExecuteChanged();
        DeleteGroupCommand.RaiseCanExecuteChanged();
        CreateSubjectCommand.RaiseCanExecuteChanged();
        UpdateSubjectCommand.RaiseCanExecuteChanged();
        DeleteSubjectCommand.RaiseCanExecuteChanged();
        CreateScheduleCommand.RaiseCanExecuteChanged();
        UpdateScheduleCommand.RaiseCanExecuteChanged();
        DeleteScheduleCommand.RaiseCanExecuteChanged();
        GenerateQrCommand.RaiseCanExecuteChanged();
        ToggleQrStateCommand.RaiseCanExecuteChanged();
        ScanAttendanceCommand.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}