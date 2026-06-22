# Техническое задание на курсовой проект

## «Проектирование базы данных и разработка серверного REST API для автоматизации учебной платформы колледжа с ролями Администратор/Студент и QR-учётом посещаемости»

---

## 1. Общие положения

### 1.1 Наименование системы
**CollegeHub** — автоматизированная информационная система управления учебным процессом колледжа.

### 1.2 Основание для разработки
Курсовой проект по МДК 07.01 «Управление и автоматизация баз данных», утверждённый приказом № 149/91к от 28.01.2026.

### 1.3 Цель работы
Разработать базу данных и серверное REST API для автоматизации учёта посещаемости студентов, управления расписанием и ролевым доступом. WPF-приложение создаётся исключительно как клиент-демонстратор для визуализации работы API.

### 1.4 Технологический стек
| Компонент | Технология |
|-----------|------------|
| СУБД | Microsoft SQL Server |
| Серверное API | Python + FastAPI |
| Контейнеризация БД | Docker |
| Клиент-демонстратор | WPF (.NET) на C# |

---

## 2. Анализ требований и постановка задачи

### 2.1 Акторы системы

| Актор | Описание |
|-------|----------|
| **Администратор** | Сотрудник колледжа, управляющий пользователями, группами, расписанием и генерацией QR-кодов |
| **Студент** | Обучающийся, который авторизуется, просматривает расписание и отмечает посещаемость по QR-коду |
| **Преподаватель** (опционально) | Может просматривать посещаемость своей группы, но в текущей версии роли ограничены администратором и студентом |

### 2.2 Функциональные требования

#### 2.2.1 Администратор
- Аутентификация и авторизация (JWT)
- CRUD пользователей (студенты, преподаватели)
- CRUD групп
- CRUD предметов
- Формирование расписания (привязка группы, предмета, преподавателя, аудитории, времени)
- Генерация QR-кода на конкретное занятие (с указанием времени действия)
- Просмотр статистики посещаемости по группам и студентам
- Деактивация QR-сессии

#### 2.2.2 Студент
- Аутентификация и авторизация (JWT)
- Просмотр своего расписания
- Сканирование QR-кода (передача идентификатора сессии на сервер)
- Фиксация отметки о посещении (статус: present / late / absent — определяется автоматически на основе времени сканирования относительно начала занятия)
- Просмотр своей истории посещаемости

#### 2.2.3 Общие требования
- Все запросы к API защищены JWT-токенами (кроме эндпоинтов аутентификации)
- Логирование действий пользователей
- Валидация входных данных
- Обработка ошибок с возвратом осмысленных HTTP-статусов

### 2.3 Нефункциональные требования
- **Производительность**: время ответа API ≤ 500 мс при 100 concurrent запросах
- **Безопасность**: хранение паролей в виде хеша (bcrypt или PBKDF2)
- **Масштабируемость**: БД и API должны допускать горизонтальное масштабирование
- **Документирование**: OpenAPI (Swagger) для API

---

## 3. Проектирование базы данных

### 3.1 Логическая модель (ER-диаграмма)

**Сущности и связи:**

```
roles (1) ────< (N) users
groups (1) ────< (N) users
users (1) ────< (N) subjects  (как teacher)
users (1) ────< (N) schedule  (как teacher)
groups (1) ────< (N) schedule
subjects (1) ────< (N) schedule
schedule (1) ────< (N) qr_sessions
qr_sessions (1) ────< (N) attendance
users (1) ────< (N) attendance
```

**Бизнес-правила:**
- Один студент может состоять только в одной группе (на момент учёта).
- Одно занятие в расписании уникально для группы, предмета, дня недели и времени (составной ключ).
- QR-сессия генерируется для конкретного занятия и имеет ограниченное время жизни (по умолчанию 15 минут до начала занятия и 15 минут после).
- Один студент может отметить присутствие на одной QR-сессии только один раз (уникальность `(user_id, qr_session_id)`).

### 3.2 Физическая модель (MS SQL Server)

Ниже представлен доработанный скрипт с **полным набором ограничений**: первичные ключи, внешние ключи с каскадными операциями, CHECK-ограничения, уникальности, индексы для ускорения запросов.

```sql
-- ============================================
-- CollegeHub: Инициализация базы данных
-- для Microsoft SQL Server (полная версия)
-- ============================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'CollegeHub')
BEGIN
    CREATE DATABASE CollegeHub;
END;
GO

USE CollegeHub;
GO

-- 1. Роли
CREATE TABLE roles (
    id INT IDENTITY(1,1) NOT NULL,
    name NVARCHAR(50) NOT NULL,
    CONSTRAINT pk_roles PRIMARY KEY (id),
    CONSTRAINT uq_roles_name UNIQUE (name)
);

-- 2. Группы
CREATE TABLE groups (
    id INT IDENTITY(1,1) NOT NULL,
    name NVARCHAR(50) NOT NULL,
    course INT NOT NULL,
    department NVARCHAR(100) NOT NULL,
    CONSTRAINT pk_groups PRIMARY KEY (id),
    CONSTRAINT uq_groups_name UNIQUE (name),
    CONSTRAINT chk_groups_course CHECK (course BETWEEN 1 AND 4)
);

-- 3. Пользователи
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
        ON DELETE NO ACTION  -- нельзя удалить роль, если есть пользователи
        ON UPDATE CASCADE,
    CONSTRAINT fk_users_group FOREIGN KEY (group_id)
        REFERENCES groups(id)
        ON DELETE SET NULL   -- при удалении группы студентам проставляется NULL
        ON UPDATE CASCADE,
    CONSTRAINT chk_users_role_group CHECK (
        (role_id = (SELECT id FROM roles WHERE name = 'student') AND group_id IS NOT NULL)
        OR (role_id <> (SELECT id FROM roles WHERE name = 'student') AND group_id IS NULL)
    )
);

-- 4. Предметы
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

-- 5. Расписание
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
    CONSTRAINT chk_schedule_day CHECK (day_of_week IN ('Monday','Tuesday','Wednesday','Thursday','Friday','Saturday')),
    CONSTRAINT chk_schedule_lesson_type CHECK (lesson_type IN ('lecture','practice','lab')),
    CONSTRAINT chk_schedule_time CHECK (start_time < end_time)
);

-- 6. QR-сессии
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

-- 7. Посещаемость
CREATE TABLE attendance (
    id INT IDENTITY(1,1) NOT NULL,
    user_id INT NOT NULL,
    qr_session_id INT NOT NULL,
    scanned_at DATETIME2 DEFAULT GETDATE(),
    status NVARCHAR(10) NOT NULL DEFAULT 'present',
    CONSTRAINT pk_attendance PRIMARY KEY (id),
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

-- 8. Индексы для производительности
CREATE INDEX idx_users_role_id ON users(role_id);
CREATE INDEX idx_users_group_id ON users(group_id);
CREATE INDEX idx_schedule_group_id ON schedule(group_id);
CREATE INDEX idx_schedule_teacher_id ON schedule(teacher_id);
CREATE INDEX idx_qr_sessions_schedule_id ON qr_sessions(schedule_id);
CREATE INDEX idx_attendance_user_id ON attendance(user_id);
CREATE INDEX idx_attendance_qr_session_id ON attendance(qr_session_id);
CREATE INDEX idx_attendance_scanned_at ON attendance(scanned_at);

GO

-- Предзаполнение ролей
INSERT INTO roles (name) VALUES ('admin'), ('teacher'), ('student');
GO

PRINT 'База данных CollegeHub успешно создана и инициализирована.';
```

### 3.3 Каскадные операции (обоснование)

| FK | ON DELETE | ON UPDATE | Обоснование |
|----|-----------|-----------|-------------|
| `users → roles` | `NO ACTION` | `CASCADE` | Нельзя удалить роль, пока есть пользователи с этой ролью |
| `users → groups` | `SET NULL` | `CASCADE` | При удалении группы студенты остаются, но без группы |
| `subjects → users` | `SET NULL` | `CASCADE` | При удалении преподавателя предмет остаётся |
| `schedule → groups` | `CASCADE` | `CASCADE` | Расписание удаляется вместе с группой |
| `schedule → subjects` | `CASCADE` | `CASCADE` | Расписание удаляется вместе с предметом |
| `schedule → users` | `SET NULL` | `CASCADE` | Преподаватель может быть удалён из расписания |
| `qr_sessions → schedule` | `CASCADE` | `CASCADE` | QR-сессии удаляются вместе с занятием |
| `attendance → users` | `CASCADE` | `CASCADE` | Посещаемость удаляется вместе со студентом |
| `attendance → qr_sessions` | `CASCADE` | `CASCADE` | Посещаемость удаляется вместе с QR-сессией |

---

## 4. UML-диаграммы

### 4.1 Диаграмма вариантов использования (Use Case)

**Акторы:** Администратор, Студент.

**Варианты использования:**

| ID | Прецедент | Актор | Описание |
|----|-----------|-------|----------|
| UC-01 | Вход в систему | Администратор, Студент | Аутентификация по email и паролю |
| UC-02 | Управление пользователями | Администратор | CRUD студентов и преподавателей |
| UC-03 | Управление группами | Администратор | CRUD групп |
| UC-04 | Управление предметами | Администратор | CRUD предметов |
| UC-05 | Формирование расписания | Администратор | Добавление/редактирование/удаление занятий |
| UC-06 | Генерация QR-кода | Администратор | Создание QR-сессии для конкретного занятия |
| UC-07 | Просмотр расписания | Студент | Просмотр своего расписания на неделю |
| UC-08 | Отметка посещаемости | Студент | Сканирование QR-кода и фиксация отметки |
| UC-09 | Просмотр истории посещаемости | Студент | Просмотр своих отметок |
| UC-10 | Просмотр статистики | Администратор | Статистика посещаемости по группам |

### 4.2 Диаграмма последовательности (Sequence) — сценарий "Отметка посещаемости"

```
Студент -> WPF-клиент: сканирует QR-код
WPF-клиент -> API: POST /attendance/scan {qr_code_data, user_id}
API -> БД: SELECT * FROM qr_sessions WHERE qr_code_data = ...
БД -> API: возвращает сессию
API -> API: проверка is_active и expires_at
API -> БД: INSERT INTO attendance (user_id, qr_session_id, status)
БД -> API: подтверждение
API -> WPF-клиент: 200 OK {status: "present"}
WPF-клиент -> Студент: отображение успеха
```

### 4.3 Диаграмма последовательности — сценарий "Генерация QR-кода"

```
Администратор -> WPF-клиент: выбирает занятие и нажимает "Сгенерировать QR"
WPF-клиент -> API: POST /qr/generate {schedule_id, expires_minutes}
API -> БД: SELECT * FROM schedule WHERE id = ...
API -> БД: INSERT INTO qr_sessions (schedule_id, qr_code_data, expires_at)
API -> API: генерация уникального QR-кода (UUID)
БД -> API: подтверждение
API -> WPF-клиент: 201 Created {qr_code_data, expires_at}
WPF-клиент -> Администратор: отображение QR-кода
```

### 4.4 ER-диаграмма (физическая модель)

См. п. 3.2 — все таблицы, атрибуты, первичные и внешние ключи, ограничения.

---

## 5. Проектирование REST API (FastAPI)

### 5.1 Структура эндпоинтов

| Метод | Эндпоинт | Описание | Доступ |
|-------|----------|----------|--------|
| POST | `/auth/login` | Вход (возвращает JWT) | public |
| POST | `/auth/refresh` | Обновление токена | public |
| GET | `/users/me` | Профиль текущего пользователя | user |
| GET | `/users` | Список пользователей (с фильтрами) | admin |
| POST | `/users` | Создание пользователя | admin |
| PUT | `/users/{id}` | Обновление пользователя | admin |
| DELETE | `/users/{id}` | Удаление пользователя | admin |
| GET | `/groups` | Список групп | admin |
| POST | `/groups` | Создание группы | admin |
| PUT | `/groups/{id}` | Обновление группы | admin |
| DELETE | `/groups/{id}` | Удаление группы | admin |
| GET | `/subjects` | Список предметов | admin |
| POST | `/subjects` | Создание предмета | admin |
| PUT | `/subjects/{id}` | Обновление предмета | admin |
| DELETE | `/subjects/{id}` | Удаление предмета | admin |
| GET | `/schedule` | Расписание (фильтр по группе, дню) | user |
| POST | `/schedule` | Добавление занятия | admin |
| PUT | `/schedule/{id}` | Обновление занятия | admin |
| DELETE | `/schedule/{id}` | Удаление занятия | admin |
| POST | `/qr/generate` | Генерация QR-сессии | admin |
| POST | `/qr/deactivate/{id}` | Деактивация QR-сессии | admin |
| GET | `/qr/status/{id}` | Проверка статуса QR-сессии | user |
| POST | `/attendance/scan` | Отметка посещаемости | user |
| GET | `/attendance/my` | Моя посещаемость | user |
| GET | `/attendance/group/{group_id}` | Посещаемость по группе | admin |

### 5.2 Модели данных (Pydantic)

```python
# schemas.py

class UserCreate(BaseModel):
    full_name: str
    email: str
    password: str
    role_name: str  # 'admin', 'teacher', 'student'
    group_id: Optional[int] = None

class UserOut(BaseModel):
    id: int
    full_name: str
    email: str
    role: str
    group: Optional[str]
    created_at: datetime

class ScheduleCreate(BaseModel):
    group_id: int
    subject_id: int
    teacher_id: Optional[int]
    room: str
    start_time: time
    end_time: time
    day_of_week: Literal['Monday','Tuesday','Wednesday','Thursday','Friday','Saturday']
    lesson_type: Literal['lecture','practice','lab'] = 'lecture'

class QRGenerateRequest(BaseModel):
    schedule_id: int
    expires_minutes: int = 30  # время жизни QR-кода

class AttendanceScanRequest(BaseModel):
    qr_code_data: str
```

### 5.3 Аутентификация и авторизация

- JWT-токен с payload: `{user_id, role, exp}`
- Зависимости FastAPI для проверки ролей:
  - `get_current_user()` — проверяет токен, возвращает `User`
  - `require_role(role)` — декоратор/зависимость для проверки роли

---

## 6. Разработка клиента-демонстратора (WPF)

### 6.1 Назначение
WPF-приложение является **исключительно демонстрационным клиентом** для визуализации работы API. Оно не содержит бизнес-логики, вся логика вынесена на сервер.

### 6.2 Функциональные экраны

| Экран | Описание |
|-------|----------|
| **Вход** | Поля email/password, кнопка Login |
| **Главная (для студента)** | Расписание на неделю, кнопка "Сканировать QR" (имитация сканера — ввод кода вручную), история посещаемости |
| **Главная (для администратора)** | Панель управления: вкладки Пользователи, Группы, Предметы, Расписание, QR-генерация, Статистика |
| **Генерация QR** | Выбор занятия из списка, кнопка "Сгенерировать", отображение QR-кода (как изображение или текст) |
| **Статистика** | Таблицы и графики посещаемости по группам |

### 6.3 Взаимодействие с API
- Использование `HttpClient` для отправки запросов.
- Хранение JWT-токена в `SecureStorage` или в памяти.
- Обработка ошибок и отображение уведомлений.

---

## 7. Развёртывание (Docker)

### 7.1 Контейнеризация БД
```dockerfile
# Dockerfile для MS SQL Server
FROM mcr.microsoft.com/mssql/server:2022-latest
ENV ACCEPT_EULA=Y
ENV SA_PASSWORD=YourStrong!Passw0rd
COPY ./init.sql /docker-entrypoint-initdb.d/
```

### 7.2 Docker Compose
```yaml
version: '3.8'
services:
  db:
    build: ./database
    ports:
      - "1433:1433"
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourStrong!Passw0rd
    volumes:
      - sql_data:/var/opt/mssql
  api:
    build: ./api
    ports:
      - "8000:8000"
    depends_on:
      - db
    environment:
      - DB_CONNECTION=Server=db;Database=CollegeHub;User Id=sa;Password=YourStrong!Passw0rd;
volumes:
  sql_data:
```

### 7.3 API-сервер (FastAPI) в контейнере
```dockerfile
FROM python:3.11-slim
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY . .
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
```

---

## 8. Требования к документации

### 8.1 Пояснительная записка (содержание)

| Раздел | Содержание |
|--------|------------|
| Введение | Цели, задачи, актуальность |
| 1. Анализ требований | 1.1 Анализ предметной области; 1.2 UML-диаграммы (Use Case, Sequence); 1.3 Макет приложения |
| 2. Проектирование БД | 2.1 ER-диаграмма; 2.2 Физическая модель (скрипт SQL); 2.3 Связи и ограничения |
| 3. Проектирование приложения | 3.1 Прототип WPF; 3.2 Описание проекта в GitHub (README, структура, инструкция по запуску) |
| Заключение | Выводы, результаты тестирования |
| Литература | Список использованных источников |

### 8.2 GitHub-репозиторий
- Структура:
  ```
  CollegeHub/
  ├── database/
  │   └── init.sql
  ├── api/
  │   ├── main.py
  │   ├── models.py
  │   ├── schemas.py
  │   ├── crud.py
  │   ├── auth.py
  │   └── requirements.txt
  ├── client/
  │   └── (WPF проект)
  ├── docker-compose.yml
  └── README.md
  ```
- README должен содержать: описание проекта, инструкцию по запуску через Docker, примеры запросов к API.

---

## 9. Критерии оценки

| Критерий | Вес |
|----------|-----|
| Полнота и корректность SQL-скрипта (FK, PK, CHECK, каскады, индексы) | 25% |
| Наличие и корректность UML-диаграмм | 20% |
| Реализация REST API (эндпоинты, JWT, валидация) | 25% |
| Работоспособность WPF-клиента (демонстрация всех сценариев) | 15% |
| Документация (пояснительная записка, README) | 10% |
| Использование Docker для БД | 5% |

---

## 10. План-график выполнения

| Этап | Срок |
|------|------|
| Анализ требований, UML-диаграммы | 1 неделя |
| Проектирование БД, написание SQL-скрипта | 1 неделя |
| Разработка API (FastAPI) | 2 недели |
| Разработка WPF-клиента | 1.5 недели |
| Контейнеризация (Docker) | 0.5 недели |
| Тестирование, отладка | 1 неделя |
| Оформление пояснительной записки | 1 неделя |
| **Итого** | **8 недель** |

---

*Задание утверждено на заседании ЦК №5 «Информационные технологии», протокол №5 от 10.12.2025.*

ПРАВИТЕЛЬСТВО САНКТ-ПЕТЕРБУРГА
КОМИТЕТ ПО НАУКЕ И ВЫСШЕЙ ШКОЛЕ

САНКТ-ПЕТЕРБУРГСКОЕ ГОСУДАРСТВЕННОЕ
БЮДЖЕТНОЕ ПРОФЕССИОНАЛЬНОЕ ОБРАЗОВАТЕЛЬНОЕ УЧРЕЖДЕНИЕ
«АКАДЕМИЯ ТРАНСПОРТНЫХ ТЕХНОЛОГИЙ»

УТВЕРЖДАЮ
Зам. директора по учебной работе
__________________ / Вишневская М.В/
«___» _______________ 2026_ г.

ЗАДАНИЕ
на выполнение курсового проекта

1. Студент Кот Андрей Ильич
Группа КИ-31
Специальность: 09.02.07 Информационные системы и программирование (базовый уровень)
Дисциплина/Междисциплинарный курс: МДК 07.01 «Управление и автоматизация баз данных»
2. Курсовой проект на тему «Проектирование базы данных и разработка серверного REST API для автоматизации учебной платформы колледжа с ролями Администратор/Студент и QR-учётом посещаемости»
утвержден приказом № 149/91к от «28» января 2026 г.
3. Структура курсового проекта включает следующие элементы:
- пояснительная записка (титульный лист, задание, содержание, введение, теоретическая/основная часть, заключение, ссылки на используемую литературу, литература).



СОДЕРЖАНИЕ ПОЯСНИТЕЛЬНОЙ ЗАПИСКИ

Введение
1 Анализ требований и постановка задачи
1.1 Анализ предметной области. 
1.2 Разработка UML диаграмм.
1.3 Разработка макета приложения.
2 Проектирование и разработка базы данных 
2.1 Разработка ER-диаграммы на основе сырых данных.
2.2 Разработка физической модели базы данных в MS SQL Server.
2.3 Разработка и именование связей и ограничений.
3 Проектирование и разработка приложения
3.1 Разработка прототипа приложения.
3.2 Разработка описания проекта в GitHub.
Заключение
Литература





Рассмотрено на заседании ЦК № 5 «Информационные технологии»
протокол № 5 от «10» декабря 2025 года
Дата выдачи задания «___» ___ 2026 года
Дата сдачи выполненного проекта «___» _______ 2026 года
Руководитель КП ________________________________________ / Кошкин В.А. /
Подпись студента ________________________________________ / Кот А.И. /

