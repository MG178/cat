using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using catprogram.Models;

namespace catprogram.Services;

public sealed class LocalDatabaseService
{
    private const string DatabaseFolderName = "CollegeHub";
    private const string DatabaseFileName = "collegehub.sqlite";

    private static readonly string DatabasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        DatabaseFolderName,
        DatabaseFileName);

    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = DatabasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared,
        ForeignKeys = true
    }.ToString();

    public string DatabaseLocation => DatabasePath;

    public void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);

        using SqliteConnection connection = new(_connectionString);
        connection.Open();

        using SqliteCommand pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA foreign_keys = ON;";
        pragmaCommand.ExecuteNonQuery();

        ExecuteBatch(connection, GetSchemaSql());
        Seed(connection);
    }

    public DashboardSnapshot GetDashboardSnapshot()
    {
        using SqliteConnection connection = CreateOpenConnection();

        int totalUsers = ExecuteScalarInt(connection, "SELECT COUNT(*) FROM users;");
        int totalStudents = ExecuteScalarInt(connection, "SELECT COUNT(*) FROM users WHERE role_id = 3;");
        int totalGroups = ExecuteScalarInt(connection, "SELECT COUNT(*) FROM groups;");
        int totalSubjects = ExecuteScalarInt(connection, "SELECT COUNT(*) FROM subjects;");
        int totalSchedules = ExecuteScalarInt(connection, "SELECT COUNT(*) FROM schedule;");
        int activeQrSessions = ExecuteScalarInt(connection, "SELECT COUNT(*) FROM qr_sessions WHERE is_active = 1 AND expires_at > CURRENT_TIMESTAMP;");
        int todayAttendance = ExecuteScalarInt(connection, "SELECT COUNT(*) FROM attendance WHERE date(scanned_at) = date('now');");

        return new DashboardSnapshot(totalUsers, totalStudents, totalGroups, totalSubjects, totalSchedules, activeQrSessions, todayAttendance, DatabasePath);
    }

    public ObservableCollection<RoleItem> GetRoles()
    {
        using SqliteConnection connection = CreateOpenConnection();
        return ReadCollection(connection, "SELECT id, name FROM roles ORDER BY id;", reader => new RoleItem(reader.GetInt32(0), reader.GetString(1)));
    }

    public ObservableCollection<GroupItem> GetGroups()
    {
        using SqliteConnection connection = CreateOpenConnection();
        return ReadCollection(connection, "SELECT id, name, course, department FROM groups ORDER BY id;", reader => new GroupItem(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3)));
    }

    public ObservableCollection<UserItem> GetUsers()
    {
        using SqliteConnection connection = CreateOpenConnection();
        const string sql = @"
            SELECT u.id, u.full_name, u.email, u.role_id, r.name, u.group_id, g.name, u.created_at
            FROM users u
            INNER JOIN roles r ON r.id = u.role_id
            LEFT JOIN groups g ON g.id = u.group_id
            ORDER BY u.id;";

        return ReadCollection(connection, sql, reader => new UserItem(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetInt32(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetDateTime(7)));
    }

    public ObservableCollection<SubjectItem> GetSubjects()
    {
        using SqliteConnection connection = CreateOpenConnection();
        const string sql = @"
            SELECT s.id, s.name, s.teacher_id, u.full_name
            FROM subjects s
            LEFT JOIN users u ON u.id = s.teacher_id
            ORDER BY s.id;";

        return ReadCollection(connection, sql, reader => new SubjectItem(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetString(3)));
    }

    public ObservableCollection<ScheduleItem> GetSchedules()
    {
        using SqliteConnection connection = CreateOpenConnection();
        const string sql = @"
            SELECT sch.id, sch.group_id, g.name, sch.subject_id, s.name, sch.teacher_id, u.full_name,
                   sch.room, sch.start_time, sch.end_time, sch.day_of_week, sch.lesson_type
            FROM schedule sch
            INNER JOIN groups g ON g.id = sch.group_id
            INNER JOIN subjects s ON s.id = sch.subject_id
            LEFT JOIN users u ON u.id = sch.teacher_id
            ORDER BY sch.id;";

        return ReadCollection(connection, sql, reader => new ScheduleItem(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetInt32(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.GetString(11)));
    }

    public ObservableCollection<QrSessionItem> GetQrSessions()
    {
        using SqliteConnection connection = CreateOpenConnection();
        const string sql = @"
            SELECT qr.id, qr.schedule_id, g.name || ' / ' || s.name || ' / ' || sch.day_of_week || ' ' || sch.start_time,
                   qr.qr_code_data, qr.generated_at, qr.expires_at, qr.is_active
            FROM qr_sessions qr
            INNER JOIN schedule sch ON sch.id = qr.schedule_id
            INNER JOIN groups g ON g.id = sch.group_id
            INNER JOIN subjects s ON s.id = sch.subject_id
            ORDER BY qr.id DESC;";

        return ReadCollection(connection, sql, reader => new QrSessionItem(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetDateTime(4),
            reader.GetDateTime(5),
            reader.GetBoolean(6)));
    }

    public ObservableCollection<AttendanceItem> GetAttendance()
    {
        using SqliteConnection connection = CreateOpenConnection();
        const string sql = @"
            SELECT a.id, u.full_name, COALESCE(g.name, '-'), s.name, qr.qr_code_data, a.status, a.scanned_at
            FROM attendance a
            INNER JOIN users u ON u.id = a.user_id
            LEFT JOIN groups g ON g.id = u.group_id
            INNER JOIN qr_sessions qr ON qr.id = a.qr_session_id
            INNER JOIN schedule sch ON sch.id = qr.schedule_id
            INNER JOIN subjects s ON s.id = sch.subject_id
            ORDER BY a.scanned_at DESC;";

        return ReadCollection(connection, sql, reader => new AttendanceItem(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetDateTime(6)));
    }

    public bool TryLogin(string email, string password, out UserItem? user)
    {
        user = null;
        using SqliteConnection connection = CreateOpenConnection();

        const string sql = @"
            SELECT u.id, u.full_name, u.email, u.role_id, r.name, u.group_id, g.name, u.created_at, u.password_hash
            FROM users u
            INNER JOIN roles r ON r.id = u.role_id
            LEFT JOIN groups g ON g.id = u.group_id
            WHERE LOWER(u.email) = LOWER($email)
            LIMIT 1;";

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$email", email.Trim());

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return false;
        }

        string passwordHash = reader.GetString(8);
        if (!PasswordHasher.Verify(password, passwordHash))
        {
            return false;
        }

        user = new UserItem(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetInt32(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetDateTime(7));

        return true;
    }

    public UserItem CreateUser(string fullName, string email, string password, int roleId, int? groupId)
    {
        using SqliteConnection connection = CreateOpenConnection();
        string passwordHash = PasswordHasher.Hash(password);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO users (full_name, email, password_hash, role_id, group_id, created_at)
            VALUES ($full_name, $email, $password_hash, $role_id, $group_id, CURRENT_TIMESTAMP);
            SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$full_name", fullName.Trim());
        command.Parameters.AddWithValue("$email", email.Trim());
        command.Parameters.AddWithValue("$password_hash", passwordHash);
        command.Parameters.AddWithValue("$role_id", roleId);
        command.Parameters.AddWithValue("$group_id", (object?)groupId ?? DBNull.Value);

        long id = (long)command.ExecuteScalar()!;
        return GetUserById(connection, (int)id);
    }

    public UserItem UpdateUser(int id, string fullName, string email, string? password, int roleId, int? groupId)
    {
        using SqliteConnection connection = CreateOpenConnection();
        using SqliteCommand command = connection.CreateCommand();

        if (!string.IsNullOrWhiteSpace(password))
        {
            command.CommandText = @"
                UPDATE users
                SET full_name = $full_name,
                    email = $email,
                    password_hash = $password_hash,
                    role_id = $role_id,
                    group_id = $group_id
                WHERE id = $id;";
            command.Parameters.AddWithValue("$password_hash", PasswordHasher.Hash(password));
        }
        else
        {
            command.CommandText = @"
                UPDATE users
                SET full_name = $full_name,
                    email = $email,
                    role_id = $role_id,
                    group_id = $group_id
                WHERE id = $id;";
        }

        command.Parameters.AddWithValue("$full_name", fullName.Trim());
        command.Parameters.AddWithValue("$email", email.Trim());
        command.Parameters.AddWithValue("$role_id", roleId);
        command.Parameters.AddWithValue("$group_id", (object?)groupId ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();

        return GetUserById(connection, id);
    }

    public void DeleteUser(int id)
    {
        using SqliteConnection connection = CreateOpenConnection();
        ExecuteNonQuery(connection, "DELETE FROM users WHERE id = $id;", new Dictionary<string, object?> { ["$id"] = id });
    }

    public GroupItem CreateGroup(string name, int course, string department)
    {
        using SqliteConnection connection = CreateOpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO groups (name, course, department)
            VALUES ($name, $course, $department);
            SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$name", name.Trim());
        command.Parameters.AddWithValue("$course", course);
        command.Parameters.AddWithValue("$department", department.Trim());

        long id = (long)command.ExecuteScalar()!;
        return GetGroupById(connection, (int)id);
    }

    public GroupItem UpdateGroup(int id, string name, int course, string department)
    {
        using SqliteConnection connection = CreateOpenConnection();
        ExecuteNonQuery(connection, @"
            UPDATE groups SET name = $name, course = $course, department = $department WHERE id = $id;",
            new Dictionary<string, object?>
            {
                ["$name"] = name.Trim(),
                ["$course"] = course,
                ["$department"] = department.Trim(),
                ["$id"] = id
            });

        return GetGroupById(connection, id);
    }

    public void DeleteGroup(int id)
    {
        using SqliteConnection connection = CreateOpenConnection();
        ExecuteNonQuery(connection, "DELETE FROM groups WHERE id = $id;", new Dictionary<string, object?> { ["$id"] = id });
    }

    public SubjectItem CreateSubject(string name, int? teacherId)
    {
        using SqliteConnection connection = CreateOpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO subjects (name, teacher_id)
            VALUES ($name, $teacher_id);
            SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$name", name.Trim());
        command.Parameters.AddWithValue("$teacher_id", (object?)teacherId ?? DBNull.Value);

        long id = (long)command.ExecuteScalar()!;
        return GetSubjectById(connection, (int)id);
    }

    public SubjectItem UpdateSubject(int id, string name, int? teacherId)
    {
        using SqliteConnection connection = CreateOpenConnection();
        ExecuteNonQuery(connection, @"
            UPDATE subjects SET name = $name, teacher_id = $teacher_id WHERE id = $id;",
            new Dictionary<string, object?>
            {
                ["$name"] = name.Trim(),
                ["$teacher_id"] = (object?)teacherId ?? DBNull.Value,
                ["$id"] = id
            });

        return GetSubjectById(connection, id);
    }

    public void DeleteSubject(int id)
    {
        using SqliteConnection connection = CreateOpenConnection();
        ExecuteNonQuery(connection, "DELETE FROM subjects WHERE id = $id;", new Dictionary<string, object?> { ["$id"] = id });
    }

    public ScheduleItem CreateSchedule(int groupId, int subjectId, int? teacherId, string room, string startTime, string endTime, string dayOfWeek, string lessonType)
    {
        using SqliteConnection connection = CreateOpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO schedule (group_id, subject_id, teacher_id, room, start_time, end_time, day_of_week, lesson_type)
            VALUES ($group_id, $subject_id, $teacher_id, $room, $start_time, $end_time, $day_of_week, $lesson_type);
            SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$group_id", groupId);
        command.Parameters.AddWithValue("$subject_id", subjectId);
        command.Parameters.AddWithValue("$teacher_id", (object?)teacherId ?? DBNull.Value);
        command.Parameters.AddWithValue("$room", room.Trim());
        command.Parameters.AddWithValue("$start_time", startTime.Trim());
        command.Parameters.AddWithValue("$end_time", endTime.Trim());
        command.Parameters.AddWithValue("$day_of_week", dayOfWeek.Trim());
        command.Parameters.AddWithValue("$lesson_type", lessonType.Trim());

        long id = (long)command.ExecuteScalar()!;
        return GetScheduleById(connection, (int)id);
    }

    public ScheduleItem UpdateSchedule(int id, int groupId, int subjectId, int? teacherId, string room, string startTime, string endTime, string dayOfWeek, string lessonType)
    {
        using SqliteConnection connection = CreateOpenConnection();
        ExecuteNonQuery(connection, @"
            UPDATE schedule
            SET group_id = $group_id,
                subject_id = $subject_id,
                teacher_id = $teacher_id,
                room = $room,
                start_time = $start_time,
                end_time = $end_time,
                day_of_week = $day_of_week,
                lesson_type = $lesson_type
            WHERE id = $id;",
            new Dictionary<string, object?>
            {
                ["$group_id"] = groupId,
                ["$subject_id"] = subjectId,
                ["$teacher_id"] = (object?)teacherId ?? DBNull.Value,
                ["$room"] = room.Trim(),
                ["$start_time"] = startTime.Trim(),
                ["$end_time"] = endTime.Trim(),
                ["$day_of_week"] = dayOfWeek.Trim(),
                ["$lesson_type"] = lessonType.Trim(),
                ["$id"] = id
            });

        return GetScheduleById(connection, id);
    }

    public void DeleteSchedule(int id)
    {
        using SqliteConnection connection = CreateOpenConnection();
        ExecuteNonQuery(connection, "DELETE FROM schedule WHERE id = $id;", new Dictionary<string, object?> { ["$id"] = id });
    }

    public QrSessionItem GenerateQrSession(int scheduleId, int expiresMinutes)
    {
        using SqliteConnection connection = CreateOpenConnection();
        ScheduleItem schedule = GetScheduleById(connection, scheduleId);
        string qrData = $"CH-{schedule.Id}-{Guid.NewGuid():N}".ToUpperInvariant();
        DateTime generatedAt = DateTime.Now;
        DateTime expiresAt = generatedAt.AddMinutes(Math.Clamp(expiresMinutes, 5, 180));

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO qr_sessions (schedule_id, qr_code_data, generated_at, expires_at, is_active)
            VALUES ($schedule_id, $qr_code_data, $generated_at, $expires_at, 1);
            SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$schedule_id", scheduleId);
        command.Parameters.AddWithValue("$qr_code_data", qrData);
        command.Parameters.AddWithValue("$generated_at", generatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$expires_at", expiresAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

        long id = (long)command.ExecuteScalar()!;
        return new QrSessionItem((int)id, scheduleId, schedule.DisplayName, qrData, generatedAt, expiresAt, true);
    }

    public QrSessionItem SetQrSessionState(int id, bool isActive)
    {
        using SqliteConnection connection = CreateOpenConnection();
        ExecuteNonQuery(connection, "UPDATE qr_sessions SET is_active = $is_active WHERE id = $id;", new Dictionary<string, object?>
        {
            ["$is_active"] = isActive ? 1 : 0,
            ["$id"] = id
        });

        return GetQrSessionById(connection, id);
    }

    public AttendanceScanResult ScanAttendance(string qrCodeData, int userId)
    {
        using SqliteConnection connection = CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        using SqliteCommand lookupCommand = connection.CreateCommand();
        lookupCommand.Transaction = transaction;
        lookupCommand.CommandText = @"
            SELECT qr.id, qr.schedule_id, qr.expires_at, qr.is_active, sch.start_time, sch.end_time, sch.day_of_week, sch.lesson_type, u.group_id
            FROM qr_sessions qr
            INNER JOIN schedule sch ON sch.id = qr.schedule_id
            INNER JOIN users u ON u.id = $user_id
            WHERE qr.qr_code_data = $qr_code_data
            LIMIT 1;";
        lookupCommand.Parameters.AddWithValue("$qr_code_data", qrCodeData.Trim());
        lookupCommand.Parameters.AddWithValue("$user_id", userId);

        using SqliteDataReader reader = lookupCommand.ExecuteReader();
        if (!reader.Read())
        {
            return new AttendanceScanResult(false, "QR-код не найден.", null);
        }

        int qrSessionId = reader.GetInt32(0);
        string expiresAtText = reader.GetString(2);
        bool isActive = reader.GetBoolean(3);
        string startTimeText = reader.GetString(4);
        string endTimeText = reader.GetString(5);
        string dayOfWeek = reader.GetString(6);
        string lessonType = reader.GetString(7);

        if (!isActive)
        {
            return new AttendanceScanResult(false, "QR-сессия неактивна.", null);
        }

        DateTime expiresAt = DateTime.Parse(expiresAtText, CultureInfo.InvariantCulture);
        if (expiresAt < DateTime.Now)
        {
            return new AttendanceScanResult(false, "Время действия QR-сессии истекло.", null);
        }

        DayOfWeek currentDay = DateTime.Now.DayOfWeek;
        string currentDayName = CultureInfo.InvariantCulture.DateTimeFormat.DayNames[(int)currentDay];
        if (!string.Equals(currentDayName, dayOfWeek, StringComparison.OrdinalIgnoreCase))
        {
            return new AttendanceScanResult(false, "Сканирование доступно только в день занятия.", null);
        }

        TimeOnly startTime = TimeOnly.Parse(startTimeText, CultureInfo.InvariantCulture);
        TimeOnly endTime = TimeOnly.Parse(endTimeText, CultureInfo.InvariantCulture);
        TimeOnly scanTime = TimeOnly.FromDateTime(DateTime.Now);

        string status = scanTime <= startTime.AddMinutes(10)
            ? "present"
            : scanTime <= endTime
                ? "late"
                : "absent";

        using SqliteCommand insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = @"
            INSERT INTO attendance (user_id, qr_session_id, scanned_at, status)
            VALUES ($user_id, $qr_session_id, $scanned_at, $status);";
        insertCommand.Parameters.AddWithValue("$user_id", userId);
        insertCommand.Parameters.AddWithValue("$qr_session_id", qrSessionId);
        insertCommand.Parameters.AddWithValue("$scanned_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        insertCommand.Parameters.AddWithValue("$status", status);

        try
        {
            insertCommand.ExecuteNonQuery();
            transaction.Commit();
            return new AttendanceScanResult(true, $"Посещение зафиксировано: {status}.", status);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            transaction.Rollback();
            return new AttendanceScanResult(false, "Студент уже отмечен на этой QR-сессии.", null);
        }
    }

    public string GetLatestQrCode()
    {
        using SqliteConnection connection = CreateOpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT qr_code_data FROM qr_sessions ORDER BY id DESC LIMIT 1;";
        object? value = command.ExecuteScalar();
        return value?.ToString() ?? string.Empty;
    }

    private static void ExecuteBatch(SqliteConnection connection, string sql)
    {
        foreach (string block in sql.Split("GO", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = block;
            command.ExecuteNonQuery();
        }
    }

    private static string GetSchemaSql()
    {
        return @"
            CREATE TABLE IF NOT EXISTS roles (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS groups (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                course INTEGER NOT NULL CHECK (course BETWEEN 1 AND 4),
                department TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS users (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                full_name TEXT NOT NULL,
                email TEXT NOT NULL UNIQUE,
                password_hash TEXT NOT NULL,
                role_id INTEGER NOT NULL,
                group_id INTEGER NULL,
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CHECK ((role_id = 3 AND group_id IS NOT NULL) OR (role_id <> 3 AND group_id IS NULL)),
                FOREIGN KEY(role_id) REFERENCES roles(id) ON DELETE NO ACTION ON UPDATE CASCADE,
                FOREIGN KEY(group_id) REFERENCES groups(id) ON DELETE SET NULL ON UPDATE CASCADE
            );

            CREATE TABLE IF NOT EXISTS subjects (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                teacher_id INTEGER NULL,
                FOREIGN KEY(teacher_id) REFERENCES users(id) ON DELETE SET NULL ON UPDATE CASCADE
            );

            CREATE TABLE IF NOT EXISTS schedule (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                group_id INTEGER NOT NULL,
                subject_id INTEGER NOT NULL,
                teacher_id INTEGER NULL,
                room TEXT NOT NULL,
                start_time TEXT NOT NULL,
                end_time TEXT NOT NULL,
                day_of_week TEXT NOT NULL,
                lesson_type TEXT NOT NULL DEFAULT 'lecture',
                UNIQUE (group_id, day_of_week, start_time),
                FOREIGN KEY(group_id) REFERENCES groups(id) ON DELETE CASCADE ON UPDATE CASCADE,
                FOREIGN KEY(subject_id) REFERENCES subjects(id) ON DELETE CASCADE ON UPDATE CASCADE,
                FOREIGN KEY(teacher_id) REFERENCES users(id) ON DELETE SET NULL ON UPDATE CASCADE,
                CHECK (start_time < end_time),
                CHECK (day_of_week IN ('Monday','Tuesday','Wednesday','Thursday','Friday','Saturday')),
                CHECK (lesson_type IN ('lecture','practice','lab'))
            );

            CREATE TABLE IF NOT EXISTS qr_sessions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                schedule_id INTEGER NOT NULL,
                qr_code_data TEXT NOT NULL UNIQUE,
                generated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                expires_at TEXT NOT NULL,
                is_active INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY(schedule_id) REFERENCES schedule(id) ON DELETE CASCADE ON UPDATE CASCADE,
                CHECK (expires_at > generated_at)
            );

            CREATE TABLE IF NOT EXISTS attendance (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER NOT NULL,
                qr_session_id INTEGER NOT NULL,
                scanned_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                status TEXT NOT NULL DEFAULT 'present',
                UNIQUE (user_id, qr_session_id),
                FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE ON UPDATE CASCADE,
                FOREIGN KEY(qr_session_id) REFERENCES qr_sessions(id) ON DELETE CASCADE ON UPDATE CASCADE,
                CHECK (status IN ('present','late','absent'))
            );

            CREATE INDEX IF NOT EXISTS idx_users_role_id ON users(role_id);
            CREATE INDEX IF NOT EXISTS idx_users_group_id ON users(group_id);
            CREATE INDEX IF NOT EXISTS idx_schedule_group_id ON schedule(group_id);
            CREATE INDEX IF NOT EXISTS idx_schedule_teacher_id ON schedule(teacher_id);
            CREATE INDEX IF NOT EXISTS idx_qr_sessions_schedule_id ON qr_sessions(schedule_id);
            CREATE INDEX IF NOT EXISTS idx_attendance_user_id ON attendance(user_id);
            CREATE INDEX IF NOT EXISTS idx_attendance_qr_session_id ON attendance(qr_session_id);
            CREATE INDEX IF NOT EXISTS idx_attendance_scanned_at ON attendance(scanned_at);
        ";
    }

    private void Seed(SqliteConnection connection)
    {
        if (ExecuteScalarInt(connection, "SELECT COUNT(*) FROM roles;") == 0)
        {
            ExecuteNonQuery(connection, "INSERT INTO roles (name) VALUES ('admin'), ('teacher'), ('student');", null);
        }

        if (ExecuteScalarInt(connection, "SELECT COUNT(*) FROM groups;") == 0)
        {
            ExecuteNonQuery(connection, @"
                INSERT INTO groups (name, course, department) VALUES
                ('ИС-21', 2, 'Информационные системы'),
                ('ПОВТ-31', 3, 'Программная инженерия'),
                ('БД-11', 1, 'Информационные системы');", null);
        }

        if (ExecuteScalarInt(connection, "SELECT COUNT(*) FROM users;") == 0)
        {
            string adminHash = PasswordHasher.Hash("Admin123!");
            string teacherHash = PasswordHasher.Hash("Teacher123!");
            string studentHash = PasswordHasher.Hash("Student123!");

            ExecuteNonQuery(connection, @"
                INSERT INTO users (full_name, email, password_hash, role_id, group_id) VALUES
                ('Алексей Морозов', 'admin@collegehub.local', $admin_hash, 1, NULL),
                ('Ирина Соколова', 'teacher@collegehub.local', $teacher_hash, 2, NULL),
                ('Дмитрий Кузнецов', 'student@collegehub.local', $student_hash, 3, 1),
                ('Мария Орлова', 'student2@collegehub.local', $student_hash_2, 3, 2),
                ('Ольга Воронова', 'student3@collegehub.local', $student_hash_3, 3, 3);",
                new Dictionary<string, object?>
                {
                    ["$admin_hash"] = adminHash,
                    ["$teacher_hash"] = teacherHash,
                    ["$student_hash"] = studentHash,
                    ["$student_hash_2"] = PasswordHasher.Hash("Student123!"),
                    ["$student_hash_3"] = PasswordHasher.Hash("Student123!")
                });
        }

        if (ExecuteScalarInt(connection, "SELECT COUNT(*) FROM subjects;") == 0)
        {
            ExecuteNonQuery(connection, @"
                INSERT INTO subjects (name, teacher_id) VALUES
                ('Базы данных', 2),
                ('Разработка ПО', 2),
                ('Проектирование интерфейсов', 2);", null);
        }

        if (ExecuteScalarInt(connection, "SELECT COUNT(*) FROM schedule;") == 0)
        {
            ExecuteNonQuery(connection, @"
                INSERT INTO schedule (group_id, subject_id, teacher_id, room, start_time, end_time, day_of_week, lesson_type) VALUES
                (1, 1, 2, 'A-204', '08:30', '10:00', 'Monday', 'lecture'),
                (1, 2, 2, 'A-204', '10:15', '11:45', 'Monday', 'practice'),
                (2, 3, 2, 'B-102', '09:00', '10:30', 'Tuesday', 'lab'),
                (3, 1, 2, 'C-301', '11:00', '12:30', 'Wednesday', 'lecture');", null);
        }

        if (ExecuteScalarInt(connection, "SELECT COUNT(*) FROM qr_sessions;") == 0)
        {
            DateTime generatedAt = DateTime.Now.AddMinutes(-5);
            DateTime expiresAt = DateTime.Now.AddMinutes(25);
            ExecuteNonQuery(connection, @"
                INSERT INTO qr_sessions (schedule_id, qr_code_data, generated_at, expires_at, is_active) VALUES
                (1, $qr1, $generated_at, $expires_at, 1),
                (2, $qr2, $generated_at, $expires_at, 1);",
                new Dictionary<string, object?>
                {
                    ["$qr1"] = $"CH-DEMO-{Guid.NewGuid():N}".ToUpperInvariant(),
                    ["$qr2"] = $"CH-DEMO-{Guid.NewGuid():N}".ToUpperInvariant(),
                    ["$generated_at"] = generatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    ["$expires_at"] = expiresAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                });
        }

        if (ExecuteScalarInt(connection, "SELECT COUNT(*) FROM attendance;") == 0)
        {
            ExecuteNonQuery(connection, @"
                INSERT INTO attendance (user_id, qr_session_id, scanned_at, status) VALUES
                (3, 1, datetime('now', '-10 minutes'), 'present'),
                (4, 1, datetime('now', '-2 minutes'), 'late');", null);
        }
    }

    private SqliteConnection CreateOpenConnection()
    {
        SqliteConnection connection = new(_connectionString);
        connection.Open();

        using SqliteCommand pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA foreign_keys = ON;";
        pragmaCommand.ExecuteNonQuery();

        return connection;
    }

    private static int ExecuteScalarInt(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object? value = command.ExecuteScalar();
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql, Dictionary<string, object?>? parameters)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        if (parameters is not null)
        {
            foreach (KeyValuePair<string, object?> pair in parameters)
            {
                command.Parameters.AddWithValue(pair.Key, pair.Value ?? DBNull.Value);
            }
        }

        command.ExecuteNonQuery();
    }

    private static ObservableCollection<T> ReadCollection<T>(SqliteConnection connection, string sql, Func<SqliteDataReader, T> factory)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        using SqliteDataReader reader = command.ExecuteReader();
        ObservableCollection<T> items = new();

        while (reader.Read())
        {
            items.Add(factory(reader));
        }

        return items;
    }

    private UserItem GetUserById(SqliteConnection connection, int id)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT u.id, u.full_name, u.email, u.role_id, r.name, u.group_id, g.name, u.created_at
            FROM users u
            INNER JOIN roles r ON r.id = u.role_id
            LEFT JOIN groups g ON g.id = u.group_id
            WHERE u.id = $id;";
        command.Parameters.AddWithValue("$id", id);
        using SqliteDataReader reader = command.ExecuteReader();
        reader.Read();
        return new UserItem(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetDateTime(7));
    }

    private GroupItem GetGroupById(SqliteConnection connection, int id)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, course, department FROM groups WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        using SqliteDataReader reader = command.ExecuteReader();
        reader.Read();
        return new GroupItem(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3));
    }

    private SubjectItem GetSubjectById(SqliteConnection connection, int id)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT s.id, s.name, s.teacher_id, u.full_name
            FROM subjects s
            LEFT JOIN users u ON u.id = s.teacher_id
            WHERE s.id = $id;";
        command.Parameters.AddWithValue("$id", id);
        using SqliteDataReader reader = command.ExecuteReader();
        reader.Read();
        return new SubjectItem(reader.GetInt32(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetInt32(2), reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    private ScheduleItem GetScheduleById(SqliteConnection connection, int id)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT sch.id, sch.group_id, g.name, sch.subject_id, s.name, sch.teacher_id, u.full_name,
                   sch.room, sch.start_time, sch.end_time, sch.day_of_week, sch.lesson_type
            FROM schedule sch
            INNER JOIN groups g ON g.id = sch.group_id
            INNER JOIN subjects s ON s.id = sch.subject_id
            LEFT JOIN users u ON u.id = sch.teacher_id
            WHERE sch.id = $id;";
        command.Parameters.AddWithValue("$id", id);
        using SqliteDataReader reader = command.ExecuteReader();
        reader.Read();
        return new ScheduleItem(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetInt32(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.GetString(11));
    }

    private QrSessionItem GetQrSessionById(SqliteConnection connection, int id)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT qr.id, qr.schedule_id, g.name || ' / ' || s.name || ' / ' || sch.day_of_week || ' ' || sch.start_time,
                   qr.qr_code_data, qr.generated_at, qr.expires_at, qr.is_active
            FROM qr_sessions qr
            INNER JOIN schedule sch ON sch.id = qr.schedule_id
            INNER JOIN groups g ON g.id = sch.group_id
            INNER JOIN subjects s ON s.id = sch.subject_id
            WHERE qr.id = $id;";
        command.Parameters.AddWithValue("$id", id);
        using SqliteDataReader reader = command.ExecuteReader();
        reader.Read();
        return new QrSessionItem(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetDateTime(4), reader.GetDateTime(5), reader.GetBoolean(6));
    }
}