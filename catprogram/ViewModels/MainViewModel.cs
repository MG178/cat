using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using catprogram.Models;
using catprogram.Services;

namespace catprogram.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly LocalDatabaseService _database = new();

    private bool _isLoggedIn;
    private UserItem? _currentUser;
    private string _loginEmail = "admin@collegehub.local";
    private string _loginPassword = "Admin123!";
    private string _statusMessage = "Готово к работе.";
    private bool _isBusy;
    private AppSection _currentSection = AppSection.Dashboard;
    private string _scanQrCode = string.Empty;
    private string _latestQrCode = string.Empty;
    private string _expiresMinutes = "30";

    private string _newUserName = string.Empty;
    private string _newUserEmail = string.Empty;
    private string _newUserPassword = string.Empty;
    private int _newUserRoleId = 3;
    private int? _newUserGroupId;

    private string _newGroupName = string.Empty;
    private int _newGroupCourse = 1;
    private string _newGroupDepartment = string.Empty;

    private string _newSubjectName = string.Empty;
    private int? _newSubjectTeacherId = 2;

    private int? _newScheduleGroupId = 1;
    private int? _newScheduleSubjectId = 1;
    private int? _newScheduleTeacherId = 2;
    private string _newScheduleRoom = string.Empty;
    private string _newScheduleStartTime = "08:30";
    private string _newScheduleEndTime = "10:00";
    private string _newScheduleDay = "Monday";
    private string _newScheduleLessonType = "lecture";

    private UserItem? _selectedUser;
    private GroupItem? _selectedGroup;
    private SubjectItem? _selectedSubject;
    private ScheduleItem? _selectedSchedule;
    private QrSessionItem? _selectedQrSession;
    private ImageSource? _qrPreview;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? LoginSucceeded;
    public event Action? LogoutRequested;

    public MainViewModel()
    {
        _database.Initialize();

        LoginCommand = new RelayCommand(_ => Login(), _ => !IsLoggedIn);
        LogoutCommand = new RelayCommand(_ => Logout(), _ => IsLoggedIn);
        RefreshCommand = new RelayCommand(_ => RefreshAll(), _ => IsLoggedIn);
        NavigateDashboardCommand = new RelayCommand(_ => CurrentSection = AppSection.Dashboard, _ => IsLoggedIn);
        NavigateUsersCommand = new RelayCommand(_ => CurrentSection = AppSection.Users, _ => IsLoggedIn && IsAdmin);
        NavigateGroupsCommand = new RelayCommand(_ => CurrentSection = AppSection.Groups, _ => IsLoggedIn && IsAdmin);
        NavigateSubjectsCommand = new RelayCommand(_ => CurrentSection = AppSection.Subjects, _ => IsLoggedIn && IsAdmin);
        NavigateScheduleCommand = new RelayCommand(_ => CurrentSection = AppSection.Schedule, _ => IsLoggedIn && (IsAdmin || IsTeacher));
        NavigateQrCommand = new RelayCommand(_ => CurrentSection = AppSection.Qr, _ => IsLoggedIn && (IsAdmin || IsTeacher));
        NavigateAttendanceCommand = new RelayCommand(_ => CurrentSection = AppSection.Attendance, _ => IsLoggedIn);

        CreateUserCommand = new RelayCommand(_ => CreateUser(), _ => IsLoggedIn && IsAdmin);
        UpdateUserCommand = new RelayCommand(_ => UpdateUser(), _ => IsLoggedIn && IsAdmin && SelectedUser is not null);
        DeleteUserCommand = new RelayCommand(_ => DeleteUser(), _ => IsLoggedIn && IsAdmin && SelectedUser is not null);

        CreateGroupCommand = new RelayCommand(_ => CreateGroup(), _ => IsLoggedIn && IsAdmin);
        UpdateGroupCommand = new RelayCommand(_ => UpdateGroup(), _ => IsLoggedIn && IsAdmin && SelectedGroup is not null);
        DeleteGroupCommand = new RelayCommand(_ => DeleteGroup(), _ => IsLoggedIn && IsAdmin && SelectedGroup is not null);

        CreateSubjectCommand = new RelayCommand(_ => CreateSubject(), _ => IsLoggedIn && IsAdmin);
        UpdateSubjectCommand = new RelayCommand(_ => UpdateSubject(), _ => IsLoggedIn && IsAdmin && SelectedSubject is not null);
        DeleteSubjectCommand = new RelayCommand(_ => DeleteSubject(), _ => IsLoggedIn && IsAdmin && SelectedSubject is not null);

        CreateScheduleCommand = new RelayCommand(_ => CreateSchedule(), _ => IsLoggedIn && (IsAdmin || IsTeacher));
        UpdateScheduleCommand = new RelayCommand(_ => UpdateSchedule(), _ => IsLoggedIn && (IsAdmin || IsTeacher) && SelectedSchedule is not null);
        DeleteScheduleCommand = new RelayCommand(_ => DeleteSchedule(), _ => IsLoggedIn && (IsAdmin || IsTeacher) && SelectedSchedule is not null);

        GenerateQrCommand = new RelayCommand(_ => GenerateQr(), _ => IsLoggedIn && (IsAdmin || IsTeacher) && SelectedSchedule is not null);
        ToggleQrStateCommand = new RelayCommand(_ => ToggleQrState(), _ => IsLoggedIn && (IsAdmin || IsTeacher) && SelectedQrSession is not null);
        ScanAttendanceCommand = new RelayCommand(_ => ScanAttendance(), _ => IsLoggedIn && !string.IsNullOrWhiteSpace(ScanQrCode));
        CopyLatestQrCommand = new RelayCommand(_ => CopyLatestQr(), _ => !string.IsNullOrWhiteSpace(LatestQrCode));

        RefreshAll();
        CurrentSection = AppSection.Dashboard;
        IsLoggedIn = false;
        CurrentUser = null;
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
        set
        {
            if (SetField(ref _isLoggedIn, value))
            {
                OnPropertyChanged(nameof(IsAuthenticated));
                OnPropertyChanged(nameof(IsAdmin));
                OnPropertyChanged(nameof(IsTeacher));
                OnPropertyChanged(nameof(IsStudent));
                OnPropertyChanged(nameof(CanSeeAdminSections));
                RaiseCommandStates();
            }
        }
    }

    public bool IsAuthenticated => CurrentUser is not null;
    public UserItem? CurrentUser
    {
        get => _currentUser;
        set
        {
            if (SetField(ref _currentUser, value))
            {
                OnPropertyChanged(nameof(IsAuthenticated));
                OnPropertyChanged(nameof(IsAdmin));
                OnPropertyChanged(nameof(IsTeacher));
                OnPropertyChanged(nameof(IsStudent));
                OnPropertyChanged(nameof(CanSeeAdminSections));
                OnPropertyChanged(nameof(CurrentUserName));
                OnPropertyChanged(nameof(CurrentUserRole));
                RaiseCommandStates();
            }
        }
    }

    public string CurrentUserName => CurrentUser?.FullName ?? "Гость";
    public string CurrentUserRole => CurrentUser?.RoleName ?? "—";
    public bool IsAdmin => string.Equals(CurrentUser?.RoleName, "admin", StringComparison.OrdinalIgnoreCase);
    public bool IsTeacher => string.Equals(CurrentUser?.RoleName, "teacher", StringComparison.OrdinalIgnoreCase);
    public bool IsStudent => string.Equals(CurrentUser?.RoleName, "student", StringComparison.OrdinalIgnoreCase);
    public bool CanSeeAdminSections => IsLoggedIn && IsAdmin;

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

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public AppSection CurrentSection
    {
        get => _currentSection;
        set => SetField(ref _currentSection, value);
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

    public ImageSource? QrPreview
    {
        get => _qrPreview;
        set => SetField(ref _qrPreview, value);
    }

    public string NewUserName { get => _newUserName; set => SetField(ref _newUserName, value); }
    public string NewUserEmail { get => _newUserEmail; set => SetField(ref _newUserEmail, value); }
    public string NewUserPassword { get => _newUserPassword; set => SetField(ref _newUserPassword, value); }
    public int NewUserRoleId { get => _newUserRoleId; set => SetField(ref _newUserRoleId, value); }
    public int? NewUserGroupId { get => _newUserGroupId; set => SetField(ref _newUserGroupId, value); }

    public string NewGroupName { get => _newGroupName; set => SetField(ref _newGroupName, value); }
    public int NewGroupCourse { get => _newGroupCourse; set => SetField(ref _newGroupCourse, value); }
    public string NewGroupDepartment { get => _newGroupDepartment; set => SetField(ref _newGroupDepartment, value); }

    public string NewSubjectName { get => _newSubjectName; set => SetField(ref _newSubjectName, value); }
    public int? NewSubjectTeacherId { get => _newSubjectTeacherId; set => SetField(ref _newSubjectTeacherId, value); }

    public int? NewScheduleGroupId { get => _newScheduleGroupId; set => SetField(ref _newScheduleGroupId, value); }
    public int? NewScheduleSubjectId { get => _newScheduleSubjectId; set => SetField(ref _newScheduleSubjectId, value); }
    public int? NewScheduleTeacherId { get => _newScheduleTeacherId; set => SetField(ref _newScheduleTeacherId, value); }
    public string NewScheduleRoom { get => _newScheduleRoom; set => SetField(ref _newScheduleRoom, value); }
    public string NewScheduleStartTime { get => _newScheduleStartTime; set => SetField(ref _newScheduleStartTime, value); }
    public string NewScheduleEndTime { get => _newScheduleEndTime; set => SetField(ref _newScheduleEndTime, value); }
    public string NewScheduleDay { get => _newScheduleDay; set => SetField(ref _newScheduleDay, value); }
    public string NewScheduleLessonType { get => _newScheduleLessonType; set => SetField(ref _newScheduleLessonType, value); }

    public UserItem? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetField(ref _selectedUser, value) && value is not null)
            {
                NewUserName = value.FullName;
                NewUserEmail = value.Email;
                NewUserPassword = string.Empty;
                NewUserRoleId = value.RoleId;
                NewUserGroupId = value.GroupId;
            }
        }
    }
    public GroupItem? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (SetField(ref _selectedGroup, value) && value is not null)
            {
                NewGroupName = value.Name;
                NewGroupCourse = value.Course;
                NewGroupDepartment = value.Department;
            }
        }
    }
    public SubjectItem? SelectedSubject
    {
        get => _selectedSubject;
        set
        {
            if (SetField(ref _selectedSubject, value) && value is not null)
            {
                NewSubjectName = value.Name;
                NewSubjectTeacherId = value.TeacherId;
            }
        }
    }
    public ScheduleItem? SelectedSchedule
    {
        get => _selectedSchedule;
        set
        {
            if (SetField(ref _selectedSchedule, value) && value is not null)
            {
                NewScheduleGroupId = value.GroupId;
                NewScheduleSubjectId = value.SubjectId;
                NewScheduleTeacherId = value.TeacherId;
                NewScheduleRoom = value.Room;
                NewScheduleStartTime = value.StartTime;
                NewScheduleEndTime = value.EndTime;
                NewScheduleDay = value.DayOfWeek;
                NewScheduleLessonType = value.LessonType;
            }
        }
    }
    public QrSessionItem? SelectedQrSession
    {
        get => _selectedQrSession;
        set
        {
            if (SetField(ref _selectedQrSession, value) && value is not null)
            {
                ScanQrCode = value.QrCodeData;
            }
        }
    }

    public RelayCommand LoginCommand { get; }
    public RelayCommand LogoutCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand NavigateDashboardCommand { get; }
    public RelayCommand NavigateUsersCommand { get; }
    public RelayCommand NavigateGroupsCommand { get; }
    public RelayCommand NavigateSubjectsCommand { get; }
    public RelayCommand NavigateScheduleCommand { get; }
    public RelayCommand NavigateQrCommand { get; }
    public RelayCommand NavigateAttendanceCommand { get; }
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
    public RelayCommand CopyLatestQrCommand { get; }

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

        if (NewUserGroupId is null && Groups.Count > 0)
        {
            NewUserGroupId = Groups[0].Id;
        }

        if (NewSubjectTeacherId is null && Users.Count > 0)
        {
            NewSubjectTeacherId = Users.FirstOrDefault(u => IsTeacherRole(u.RoleName))?.Id ?? Users[0].Id;
        }

        if (NewScheduleGroupId is null && Groups.Count > 0)
        {
            NewScheduleGroupId = Groups[0].Id;
        }

        if (NewScheduleSubjectId is null && Subjects.Count > 0)
        {
            NewScheduleSubjectId = Subjects[0].Id;
        }

        if (NewScheduleTeacherId is null && Users.Count > 0)
        {
            NewScheduleTeacherId = Users.FirstOrDefault(u => IsTeacherRole(u.RoleName))?.Id;
        }

        LatestQrCode = _database.GetLatestQrCode();
        QrPreview = string.IsNullOrWhiteSpace(LatestQrCode) ? null : QrCodeRenderer.Render(LatestQrCode);

        OnPropertyChanged(nameof(Roles));
        OnPropertyChanged(nameof(Groups));
        OnPropertyChanged(nameof(Users));
        OnPropertyChanged(nameof(Subjects));
        OnPropertyChanged(nameof(Schedules));
        OnPropertyChanged(nameof(QrSessions));
        OnPropertyChanged(nameof(Attendance));
        OnPropertyChanged(nameof(Dashboard));
        OnPropertyChanged(nameof(LatestQrCode));
        OnPropertyChanged(nameof(QrPreview));
        OnPropertyChanged(nameof(CanSeeAdminSections));
        RaiseCommandStates();
    }

    private void Login()
    {
        try
        {
            if (_database.TryLogin(LoginEmail, LoginPassword, out UserItem? user) && user is not null)
            {
                CurrentUser = user;
                IsLoggedIn = true;
                CurrentSection = AppSection.Dashboard;
                StatusMessage = $"Вход выполнен: {user.FullName} ({user.RoleName})";
                RefreshAll();
                LoginSucceeded?.Invoke();
                return;
            }

            StatusMessage = "Неверный email или пароль.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка входа: {ex.Message}";
        }
    }

    private void Logout()
    {
        CurrentUser = null;
        IsLoggedIn = false;
        CurrentSection = AppSection.Dashboard;
        ScanQrCode = string.Empty;
        StatusMessage = "Вы вышли из системы.";
        LogoutRequested?.Invoke();
    }

    private void CreateUser()
    {
        if (!RequireAdmin()) return;
        TryAction("Пользователь создан", () =>
        {
            _database.CreateUser(NewUserName, NewUserEmail, NewUserPassword, NewUserRoleId, NormalizeStudentGroup(NewUserRoleId, NewUserGroupId));
            ClearUserForm();
        });
    }

    private void UpdateUser()
    {
        if (!RequireAdmin() || SelectedUser is null) return;
        TryAction("Пользователь обновлён", () =>
        {
            _database.UpdateUser(SelectedUser.Id, NewUserNameOrSelected(), NewUserEmailOrSelected(), string.IsNullOrWhiteSpace(NewUserPassword) ? null : NewUserPassword, NewUserRoleIdOrSelected(), NormalizeStudentGroup(NewUserRoleIdOrSelected(), NewUserGroupId));
            ClearUserForm();
        });
    }

    private void DeleteUser()
    {
        if (!RequireAdmin() || SelectedUser is null) return;
        TryAction("Пользователь удалён", () =>
        {
            _database.DeleteUser(SelectedUser.Id);
            SelectedUser = null;
            ClearUserForm();
        });
    }

    private void CreateGroup()
    {
        if (!RequireAdmin()) return;
        TryAction("Группа создана", () =>
        {
            _database.CreateGroup(NewGroupName, NewGroupCourse, NewGroupDepartment);
            ClearGroupForm();
        });
    }

    private void UpdateGroup()
    {
        if (!RequireAdmin() || SelectedGroup is null) return;
        TryAction("Группа обновлена", () =>
        {
            _database.UpdateGroup(SelectedGroup.Id, string.IsNullOrWhiteSpace(NewGroupName) ? SelectedGroup.Name : NewGroupName, NewGroupCourse == 0 ? SelectedGroup.Course : NewGroupCourse, string.IsNullOrWhiteSpace(NewGroupDepartment) ? SelectedGroup.Department : NewGroupDepartment);
            ClearGroupForm();
        });
    }

    private void DeleteGroup()
    {
        if (!RequireAdmin() || SelectedGroup is null) return;
        TryAction("Группа удалена", () =>
        {
            _database.DeleteGroup(SelectedGroup.Id);
            SelectedGroup = null;
            ClearGroupForm();
        });
    }

    private void CreateSubject()
    {
        if (!RequireAdmin()) return;
        TryAction("Предмет создан", () =>
        {
            _database.CreateSubject(NewSubjectName, NewSubjectTeacherId);
            ClearSubjectForm();
        });
    }

    private void UpdateSubject()
    {
        if (!RequireAdmin() || SelectedSubject is null) return;
        TryAction("Предмет обновлён", () =>
        {
            _database.UpdateSubject(SelectedSubject.Id, string.IsNullOrWhiteSpace(NewSubjectName) ? SelectedSubject.Name : NewSubjectName, NewSubjectTeacherId);
            ClearSubjectForm();
        });
    }

    private void DeleteSubject()
    {
        if (!RequireAdmin() || SelectedSubject is null) return;
        TryAction("Предмет удалён", () =>
        {
            _database.DeleteSubject(SelectedSubject.Id);
            SelectedSubject = null;
            ClearSubjectForm();
        });
    }

    private void CreateSchedule()
    {
        if (!RequireScheduleAccess()) return;
        TryAction("Расписание создано", () =>
        {
            _database.CreateSchedule(RequireValue(NewScheduleGroupId), RequireValue(NewScheduleSubjectId), NewScheduleTeacherId, NewScheduleRoom, NewScheduleStartTime, NewScheduleEndTime, NewScheduleDay, NewScheduleLessonType);
            ClearScheduleForm();
        });
    }

    private void UpdateSchedule()
    {
        if (!RequireScheduleAccess() || SelectedSchedule is null) return;
        TryAction("Расписание обновлено", () =>
        {
            _database.UpdateSchedule(SelectedSchedule.Id, NewScheduleGroupId ?? SelectedSchedule.GroupId, NewScheduleSubjectId ?? SelectedSchedule.SubjectId, NewScheduleTeacherId, string.IsNullOrWhiteSpace(NewScheduleRoom) ? SelectedSchedule.Room : NewScheduleRoom, string.IsNullOrWhiteSpace(NewScheduleStartTime) ? SelectedSchedule.StartTime : NewScheduleStartTime, string.IsNullOrWhiteSpace(NewScheduleEndTime) ? SelectedSchedule.EndTime : NewScheduleEndTime, string.IsNullOrWhiteSpace(NewScheduleDay) ? SelectedSchedule.DayOfWeek : NewScheduleDay, string.IsNullOrWhiteSpace(NewScheduleLessonType) ? SelectedSchedule.LessonType : NewScheduleLessonType);
            ClearScheduleForm();
        });
    }

    private void DeleteSchedule()
    {
        if (!RequireScheduleAccess() || SelectedSchedule is null) return;
        TryAction("Расписание удалено", () =>
        {
            _database.DeleteSchedule(SelectedSchedule.Id);
            SelectedSchedule = null;
            ClearScheduleForm();
        });
    }

    private void GenerateQr()
    {
        if (!RequireScheduleAccess() || SelectedSchedule is null) return;
        TryAction("QR-сессия создана", () =>
        {
            int expires = int.TryParse(ExpiresMinutes, out int minutes) ? minutes : 30;
            QrSessionItem session = _database.GenerateQrSession(SelectedSchedule.Id, expires);
            LatestQrCode = session.QrCodeData;
            QrPreview = QrCodeRenderer.Render(session.QrCodeData);
            ScanQrCode = session.QrCodeData;
            SelectedQrSession = session;
            StatusMessage = $"QR-сессия создана до {session.ExpiresAt:HH:mm}.";
        }, refresh: true);
    }

    private void ToggleQrState()
    {
        if (!RequireScheduleAccess() || SelectedQrSession is null) return;
        TryAction("Состояние QR изменено", () =>
        {
            _database.SetQrSessionState(SelectedQrSession.Id, !SelectedQrSession.IsActive);
        });
    }

    private void ScanAttendance()
    {
        if (CurrentUser is null) return;
        TryAction("Посещение отмечено", () =>
        {
            AttendanceScanResult result = _database.ScanAttendance(ScanQrCode, CurrentUser.Id);
            StatusMessage = result.Message;
        }, refresh: true);
    }

    private void CopyLatestQr()
    {
        if (string.IsNullOrWhiteSpace(LatestQrCode)) return;
        try
        {
            Clipboard.SetText(LatestQrCode);
            StatusMessage = "QR-код скопирован в буфер обмена.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не удалось скопировать QR: {ex.Message}";
        }
    }

    private void ClearUserForm()
    {
        NewUserName = string.Empty;
        NewUserEmail = string.Empty;
        NewUserPassword = string.Empty;
        if (Roles.Count > 0) NewUserRoleId = Roles.Last().Id;
        if (Groups.Count > 0) NewUserGroupId = Groups[0].Id;
    }

    private void ClearGroupForm()
    {
        NewGroupName = string.Empty;
        NewGroupCourse = 1;
        NewGroupDepartment = string.Empty;
    }

    private void ClearSubjectForm()
    {
        NewSubjectName = string.Empty;
        NewSubjectTeacherId = Users.FirstOrDefault(u => IsTeacherRole(u.RoleName))?.Id ?? Users.FirstOrDefault()?.Id;
    }

    private void ClearScheduleForm()
    {
        NewScheduleRoom = string.Empty;
        NewScheduleStartTime = "08:30";
        NewScheduleEndTime = "10:00";
        NewScheduleDay = "Monday";
        NewScheduleLessonType = "lecture";
    }

    private bool RequireAdmin()
    {
        if (!IsAdmin)
        {
            StatusMessage = "Для этого действия нужны права администратора.";
            return false;
        }
        return true;
    }

    private bool RequireScheduleAccess()
    {
        if (!(IsAdmin || IsTeacher))
        {
            StatusMessage = "Для этого раздела нужны права преподавателя или администратора.";
            return false;
        }
        return true;
    }

    private static bool IsTeacherRole(string role) => string.Equals(role, "teacher", StringComparison.OrdinalIgnoreCase);

    private static int RequireValue(int? value) => value ?? throw new InvalidOperationException("Required value is missing.");

    private static int? NormalizeStudentGroup(int roleId, int? groupId) => roleId == 3 ? groupId : null;

    private string NewUserNameOrSelected() => string.IsNullOrWhiteSpace(NewUserName) ? SelectedUser!.FullName : NewUserName;
    private string NewUserEmailOrSelected() => string.IsNullOrWhiteSpace(NewUserEmail) ? SelectedUser!.Email : NewUserEmail;
    private int NewUserRoleIdOrSelected() => NewUserRoleId == 0 ? SelectedUser!.RoleId : NewUserRoleId;

    private void TryAction(string successMessage, Action action, bool refresh = true)
    {
        try
        {
            IsBusy = true;
            action();
            if (refresh)
            {
                RefreshAll();
            }
            StatusMessage = successMessage;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RaiseCommandStates()
    {
        LoginCommand.RaiseCanExecuteChanged();
        LogoutCommand.RaiseCanExecuteChanged();
        RefreshCommand.RaiseCanExecuteChanged();
        NavigateDashboardCommand.RaiseCanExecuteChanged();
        NavigateUsersCommand.RaiseCanExecuteChanged();
        NavigateGroupsCommand.RaiseCanExecuteChanged();
        NavigateSubjectsCommand.RaiseCanExecuteChanged();
        NavigateScheduleCommand.RaiseCanExecuteChanged();
        NavigateQrCommand.RaiseCanExecuteChanged();
        NavigateAttendanceCommand.RaiseCanExecuteChanged();
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
        CopyLatestQrCommand.RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        if (propertyName is nameof(SelectedUser) or nameof(SelectedGroup) or nameof(SelectedSubject) or nameof(SelectedSchedule) or nameof(SelectedQrSession))
        {
            RaiseCommandStates();
        }
        return true;
    }

    private void OnPropertyChanged(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
