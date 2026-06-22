-- ============================================================
-- CollegeHub: Создание базы данных и всех объектов
-- для автоматизированной учебной платформы колледжа
-- Версия: 1.0 (с исправленным CHECK-ограничением в users)
-- ============================================================

-- Создание базы данных, если она не существует
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'CollegeHub')
BEGIN
    CREATE DATABASE CollegeHub;
END;
GO

USE CollegeHub;
GO

-- ============================================================
-- 1. Таблица ролей (справочник)
-- ============================================================
CREATE TABLE roles (
    id INT IDENTITY(1,1) NOT NULL,
    name NVARCHAR(50) NOT NULL,
    CONSTRAINT pk_roles PRIMARY KEY (id),
    CONSTRAINT uq_roles_name UNIQUE (name)
);
GO

-- ============================================================
-- 2. Таблица групп
-- ============================================================
CREATE TABLE groups (
    id INT IDENTITY(1,1) NOT NULL,
    name NVARCHAR(50) NOT NULL,
    course INT NOT NULL,
    department NVARCHAR(100) NOT NULL,
    CONSTRAINT pk_groups PRIMARY KEY (id),
    CONSTRAINT uq_groups_name UNIQUE (name),
    CONSTRAINT chk_groups_course CHECK (course BETWEEN 1 AND 4)
);
GO

-- ============================================================
-- 3. Таблица пользователей
-- ============================================================
CREATE TABLE users (
    id INT IDENTITY(1,1) NOT NULL,
    full_name NVARCHAR(150) NOT NULL,
    email NVARCHAR(100) NOT NULL,
    password_hash NVARCHAR(255) NOT NULL,
    role_id INT NOT NULL,
    group_id INT NULL,
    created_at DATETIME2 DEFAULT GETDATE(),
    CONSTRAINT pk_users PRIMARY KEY (id),
    CONSTRAINT uq_users_email UNIQUE (email),
    CONSTRAINT fk_users_role FOREIGN KEY (role_id)
        REFERENCES roles(id)
        ON DELETE NO ACTION   -- роль нельзя удалить, пока есть пользователи
        ON UPDATE CASCADE,
    CONSTRAINT fk_users_group FOREIGN KEY (group_id)
        REFERENCES groups(id)
        ON DELETE SET NULL    -- при удалении группы у студентов сбрасывается group_id
        ON UPDATE CASCADE,
    -- Проверка: студент обязан иметь группу, остальные роли — нет
    -- Роли вставляются в порядке: admin (1), teacher (2), student (3)
    CONSTRAINT chk_users_role_group CHECK (
        (role_id = 3 AND group_id IS NOT NULL) OR (role_id <> 3 AND group_id IS NULL)
    )
);
GO

-- ============================================================
-- 4. Таблица предметов
-- ============================================================
CREATE TABLE subjects (
    id INT IDENTITY(1,1) NOT NULL,
    name NVARCHAR(150) NOT NULL,
    teacher_id INT NULL,
    CONSTRAINT pk_subjects PRIMARY KEY (id),
    CONSTRAINT uq_subjects_name UNIQUE (name),
    CONSTRAINT fk_subjects_teacher FOREIGN KEY (teacher_id)
        REFERENCES users(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE
);
GO

-- ============================================================
-- 5. Таблица расписания
-- ============================================================
CREATE TABLE schedule (
    id INT IDENTITY(1,1) NOT NULL,
    group_id INT NOT NULL,
    subject_id INT NOT NULL,
    teacher_id INT NULL,
    room NVARCHAR(50) NOT NULL,
    start_time TIME NOT NULL,
    end_time TIME NOT NULL,
    day_of_week NVARCHAR(10) NOT NULL,
    lesson_type NVARCHAR(10) NOT NULL DEFAULT 'lecture',
    CONSTRAINT pk_schedule PRIMARY KEY (id),
    -- Уникальность: у одной группы в один день и время не может быть двух занятий
    CONSTRAINT uq_schedule_group_day_time UNIQUE (group_id, day_of_week, start_time),
    CONSTRAINT fk_schedule_group FOREIGN KEY (group_id)
        REFERENCES groups(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,
    CONSTRAINT fk_schedule_subject FOREIGN KEY (subject_id)
        REFERENCES subjects(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,
    CONSTRAINT fk_schedule_teacher FOREIGN KEY (teacher_id)
        REFERENCES users(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE,
    CONSTRAINT chk_schedule_day CHECK (day_of_week IN 
        ('Monday','Tuesday','Wednesday','Thursday','Friday','Saturday')),
    CONSTRAINT chk_schedule_lesson_type CHECK (lesson_type IN ('lecture','practice','lab')),
    CONSTRAINT chk_schedule_time CHECK (start_time < end_time)
);
GO

-- ============================================================
-- 6. Таблица QR-сессий
-- ============================================================
CREATE TABLE qr_sessions (
    id INT IDENTITY(1,1) NOT NULL,
    schedule_id INT NOT NULL,
    qr_code_data NVARCHAR(255) NOT NULL,
    generated_at DATETIME2 DEFAULT GETDATE(),
    expires_at DATETIME2 NOT NULL,
    is_active BIT DEFAULT 1,
    CONSTRAINT pk_qr_sessions PRIMARY KEY (id),
    CONSTRAINT uq_qr_sessions_data UNIQUE (qr_code_data),
    CONSTRAINT fk_qr_schedule FOREIGN KEY (schedule_id)
        REFERENCES schedule(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,
    CONSTRAINT chk_qr_expires CHECK (expires_at > generated_at)
);
GO

-- ============================================================
-- 7. Таблица посещаемости
-- ============================================================
CREATE TABLE attendance (
    id INT IDENTITY(1,1) NOT NULL,
    user_id INT NOT NULL,
    qr_session_id INT NOT NULL,
    scanned_at DATETIME2 DEFAULT GETDATE(),
    status NVARCHAR(10) NOT NULL DEFAULT 'present',
    CONSTRAINT pk_attendance PRIMARY KEY (id),
    -- Один студент может отметиться на одной QR-сессии только один раз
    CONSTRAINT uq_attendance_user_qr UNIQUE (user_id, qr_session_id),
    CONSTRAINT fk_attendance_user FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,
    CONSTRAINT fk_attendance_qr FOREIGN KEY (qr_session_id)
        REFERENCES qr_sessions(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,
    CONSTRAINT chk_attendance_status CHECK (status IN ('present','late','absent'))
);
GO

-- ============================================================
-- 8. Индексы для повышения производительности
-- ============================================================
CREATE INDEX idx_users_role_id ON users(role_id);
CREATE INDEX idx_users_group_id ON users(group_id);
CREATE INDEX idx_schedule_group_id ON schedule(group_id);
CREATE INDEX idx_schedule_teacher_id ON schedule(teacher_id);
CREATE INDEX idx_qr_sessions_schedule_id ON qr_sessions(schedule_id);
CREATE INDEX idx_attendance_user_id ON attendance(user_id);
CREATE INDEX idx_attendance_qr_session_id ON attendance(qr_session_id);
CREATE INDEX idx_attendance_scanned_at ON attendance(scanned_at);
GO

-- ============================================================
-- 9. Начальное заполнение справочника ролей
-- (порядок важен: admin=1, teacher=2, student=3)
-- ============================================================
INSERT INTO roles (name) VALUES ('admin'), ('teacher'), ('student');
GO

-- Вывод сообщения об успешном создании
PRINT 'База данных CollegeHub успешно создана и инициализирована.';
GO