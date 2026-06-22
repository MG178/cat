namespace catprogram.Models;

public sealed record DashboardSnapshot(
    int TotalUsers,
    int TotalStudents,
    int TotalGroups,
    int TotalSubjects,
    int TotalSchedules,
    int ActiveQrSessions,
    int TodayAttendance,
    string DatabasePath);

public sealed record RoleItem(int Id, string Name);

public sealed record GroupItem(int Id, string Name, int Course, string Department)
{
    public string DisplayName => $"{Name} · {Course} курс";
}

public sealed record UserItem(
    int Id,
    string FullName,
    string Email,
    int RoleId,
    string RoleName,
    int? GroupId,
    string? GroupName,
    DateTime CreatedAt)
{
    public string DisplayName => string.IsNullOrWhiteSpace(GroupName)
        ? $"{FullName} · {RoleName}"
        : $"{FullName} · {RoleName} · {GroupName}";
}

public sealed record SubjectItem(int Id, string Name, int? TeacherId, string? TeacherName)
{
    public string DisplayName => TeacherName is null ? Name : $"{Name} · {TeacherName}";
}

public sealed record ScheduleItem(
    int Id,
    int GroupId,
    string GroupName,
    int SubjectId,
    string SubjectName,
    int? TeacherId,
    string? TeacherName,
    string Room,
    string StartTime,
    string EndTime,
    string DayOfWeek,
    string LessonType)
{
    public string DisplayName => $"{GroupName} · {SubjectName} · {DayOfWeek} {StartTime}-{EndTime}";
}

public sealed record QrSessionItem(
    int Id,
    int ScheduleId,
    string ScheduleLabel,
    string QrCodeData,
    DateTime GeneratedAt,
    DateTime ExpiresAt,
    bool IsActive)
{
    public string StateText => IsActive && ExpiresAt > DateTime.Now ? "Активна" : "Неактивна";
}

public sealed record AttendanceItem(
    int Id,
    string UserName,
    string GroupName,
    string SubjectName,
    string QrCodeData,
    string Status,
    DateTime ScannedAt)
{
    public string ScannedAtText => ScannedAt.ToString("dd.MM.yyyy HH:mm");
}

public sealed record AttendanceScanResult(bool Success, string Message, string? Status);