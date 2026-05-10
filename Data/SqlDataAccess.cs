using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Computer_networks.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Windows;

namespace Computer_networks.Data
{
    public static class SqlDataAccess
    {
        // #2 ИСПРАВЛЕНО: Строка подключения читается из App.config → <connectionStrings>
        public static string ConnectionString =>
            System.Configuration.ConfigurationManager
                .ConnectionStrings["MainDB"]?.ConnectionString
            ?? "Server=localhost\\SQLEXPRESS;Database=ComputerNetworksTextbookDB;Trusted_Connection=True;TrustServerCertificate=True;";

        // #2 ИСПРАВЛЕНО: DefaultCourseId читается из App.config → <appSettings>
        private static int _currentCourseId = -1;
        public static int CurrentCourseId
        {
            get
            {
                if (_currentCourseId == -1)
                {
                    if (int.TryParse(
                        System.Configuration.ConfigurationManager.AppSettings["DefaultCourseId"],
                        out int cfgId))
                        _currentCourseId = cfgId;
                    else
                        _currentCourseId = 2;
                }
                return _currentCourseId;
            }
            set => _currentCourseId = value;
        }

        // --- БАЗОВЫЕ МЕТОДЫ ---
        public static void EnsureDbSchema()
        {
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Execute(@"
                        IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'UserSettings') AND type='U')
                        CREATE TABLE UserSettings (
                            SettingID INT IDENTITY(1,1) PRIMARY KEY,
                            UserID INT NOT NULL,
                            FontSize NVARCHAR(20) NOT NULL DEFAULT 'Medium',
                            Theme NVARCHAR(20) NOT NULL DEFAULT 'Light',
                            GlossaryOpenCount INT NOT NULL DEFAULT 0
                        );
                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'GlossaryOpenCount')
                            ALTER TABLE UserSettings ADD GlossaryOpenCount INT NOT NULL DEFAULT 0;
                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'Avatar')
                            ALTER TABLE UserSettings ADD Avatar NVARCHAR(10) NOT NULL DEFAULT '';
                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'IsEmailVerified')
                            ALTER TABLE Users ADD IsEmailVerified BIT NOT NULL DEFAULT 0;
                        IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'EmailVerifications') AND type='U')
                        CREATE TABLE EmailVerifications (
                            VerificationID INT IDENTITY(1,1) PRIMARY KEY,
                            UserID         INT NOT NULL,
                            Email          NVARCHAR(255) NOT NULL,
                            Code           NVARCHAR(10) NOT NULL,
                            CreatedAt      DATETIME NOT NULL DEFAULT GETDATE(),
                            ExpiresAt      DATETIME NOT NULL,
                            IsUsed         BIT NOT NULL DEFAULT 0
                        );

                        IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'LabWorks') AND type='U')
                        CREATE TABLE LabWorks (
                            LabWorkID INT IDENTITY(1,1) PRIMARY KEY,
                            CourseID INT NOT NULL,
                            Title NVARCHAR(300) NOT NULL,
                            Description NVARCHAR(MAX) NULL,
                            Deadline DATETIME NULL,
                            IsActive BIT NOT NULL DEFAULT 1,
                            CreatedBy INT NOT NULL,
                            CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
                        );

                        IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'LabReportSubmissions') AND type='U')
                        CREATE TABLE LabReportSubmissions (
                            SubmissionID INT IDENTITY(1,1) PRIMARY KEY,
                            LabWorkID INT NOT NULL,
                            UserID INT NOT NULL,
                            FileName NVARCHAR(260) NOT NULL,
                            StoredName NVARCHAR(260) NOT NULL,
                            FilePath NVARCHAR(500) NOT NULL,
                            FileSizeKB INT NOT NULL DEFAULT 0,
                            Comment NVARCHAR(500) NULL,
                            UploadedAt DATETIME NOT NULL DEFAULT GETDATE()
                        );

                        IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'LabWorks') AND type='U')
                        BEGIN
                            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LabWorks') AND name = 'Deadline')
                                ALTER TABLE LabWorks ADD Deadline DATETIME NULL;
                            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LabWorks') AND name = 'IsActive')
                                ALTER TABLE LabWorks ADD IsActive BIT NOT NULL CONSTRAINT DF_LabWorks_IsActive DEFAULT 1;
                            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LabWorks') AND name = 'CreatedBy')
                                ALTER TABLE LabWorks ADD CreatedBy INT NOT NULL CONSTRAINT DF_LabWorks_CreatedBy DEFAULT 0;
                            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LabWorks') AND name = 'CreatedAt')
                                ALTER TABLE LabWorks ADD CreatedAt DATETIME NOT NULL CONSTRAINT DF_LabWorks_CreatedAt DEFAULT GETDATE();
                            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LabWorks') AND name = 'FileName')
                                ALTER TABLE LabWorks ADD FileName NVARCHAR(260) NULL;
                            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LabWorks') AND name = 'StoredName')
                                ALTER TABLE LabWorks ADD StoredName NVARCHAR(260) NULL;
                            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LabWorks') AND name = 'FilePath')
                                ALTER TABLE LabWorks ADD FilePath NVARCHAR(500) NULL;
                            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LabWorks') AND name = 'FileSizeKB')
                                ALTER TABLE LabWorks ADD FileSizeKB INT NULL;
                            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LabWorks') AND name = 'FileUploadedAt')
                                ALTER TABLE LabWorks ADD FileUploadedAt DATETIME NULL;
                        END;

                        IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'LabReportSubmissions') AND type='U')
                        BEGIN
                            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LabReportSubmissions') AND name = 'Comment')
                                ALTER TABLE LabReportSubmissions ADD Comment NVARCHAR(500) NULL;
                            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LabReportSubmissions') AND name = 'UploadedAt')
                                ALTER TABLE LabReportSubmissions ADD UploadedAt DATETIME NOT NULL CONSTRAINT DF_LabReportSubmissions_UploadedAt DEFAULT GETDATE();
                        END;
                        ");
                }
                System.Diagnostics.Debug.WriteLine("[EnsureDbSchema] OK");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[EnsureDbSchema] ОШИБКА: {ex.Message}"); }
        }

        public static void ExecuteSql(string sql, object parameters = null)
        {
            using (IDbConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Execute(sql, parameters);
            }
        }

        public static List<T> LoadData<T>(string sql, object parameters = null)
        {
            using (IDbConnection connection = new SqlConnection(ConnectionString))
            {
                return connection.Query<T>(sql, parameters).ToList();
            }
        }

        // =============================================
        // МЕТОДЫ ДЛЯ КУРСОВ
        // =============================================
        public static List<Course> GetAllCourses()
        {
            const string sql = @"
        SELECT CourseID, CourseName, CourseDescription, IsActive, CreatedAt, UpdatedAt
        FROM Courses
        WHERE IsActive = 1
        ORDER BY CourseID";
            return LoadData<Course>(sql);
        }

        public static Course GetCourseById(int courseId)
        {
            const string sql = @"
                SELECT CourseID, CourseName, CourseDescription, IsActive, CreatedAt, UpdatedAt
                FROM Courses
                WHERE CourseID = @CourseId";
            return LoadData<Course>(sql, new { CourseId = courseId }).FirstOrDefault();
        }

        public static List<Course> GetAllCoursesIncludeInactive()
        {
            const string sql = @"
        SELECT CourseID, CourseName, CourseDescription, IsActive, CreatedAt, UpdatedAt
        FROM Courses
        ORDER BY IsActive DESC, CourseID";
            return LoadData<Course>(sql);
        }

        public static int AddCourse(string name, string description)
        {
            using (var conn = new SqlConnection(ConnectionString))
            {
                return conn.QueryFirstOrDefault<int>(@"
                    INSERT INTO Courses (CourseName, CourseDescription, IsActive, CreatedAt, UpdatedAt)
                    VALUES (@Name, @Desc, 1, GETDATE(), GETDATE());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new { Name = name, Desc = description });
            }
        }

        public static void UpdateCourseInfo(int courseId, string name, string description)
        {
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Execute(@"
                    UPDATE Courses
                    SET CourseName = @Name, CourseDescription = @Desc, UpdatedAt = GETDATE()
                    WHERE CourseID = @CourseID",
                    new { Name = name, Desc = description, CourseID = courseId });
            }
        }

        public static void SetCourseActive(int courseId, bool isActive)
        {
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Execute("UPDATE Courses SET IsActive = @Active, UpdatedAt = GETDATE() WHERE CourseID = @CourseID",
                    new { Active = isActive, CourseID = courseId });
            }
        }

        // =============================================
        // МЕТОДЫ ДЛЯ ТЕМ (С ПОДДЕРЖКОЙ КУРСОВ)
        // =============================================
        public static List<Topic> GetAllTopics(int? courseId = null)
        {
            if (courseId == null) courseId = CurrentCourseId;

            const string sql = @"
                SELECT TopicID, Title, Content, ParentTopicID, OrderIndex, CourseID
                FROM Topics 
                WHERE CourseID = @CourseId
                ORDER BY ISNULL(ParentTopicID, 0) ASC, OrderIndex ASC";

            return LoadData<Topic>(sql, new { CourseId = courseId });
        }

        public static Topic GetTopicById(int topicId)
        {
            const string sql = @"
                SELECT TopicID, Title, Content, ParentTopicID, OrderIndex, CourseID
                FROM Topics 
                WHERE TopicID = @TopicId";
            return LoadData<Topic>(sql, new { TopicId = topicId }).FirstOrDefault();
        }

        public static void UpdateTopic(Topic topic)
        {
            const string sql = @"
                UPDATE Topics 
                SET Title = @Title, Content = @Content, OrderIndex = @OrderIndex
                WHERE TopicID = @TopicID";
            ExecuteSql(sql, topic);
        }

        public static int InsertTopic(Topic topic)
        {
            const string sql = @"
                INSERT INTO Topics (Title, Content, ParentTopicID, OrderIndex, CourseID) 
                OUTPUT INSERTED.TopicID 
                VALUES (@Title, @Content, @ParentTopicID, @OrderIndex, @CourseID)";

            using (var connection = new SqlConnection(ConnectionString))
            {
                return connection.QuerySingle<int>(sql, topic);
            }
        }

        public static void DeleteTopic(int topicId)
        {
            const string deleteRelatedSql = @"
                DELETE FROM Answers WHERE QuestionID IN (SELECT QuestionID FROM Questions WHERE TopicID = @TopicId);
                DELETE FROM ReviewLog WHERE TopicID = @TopicId;
                DELETE FROM Bookmarks WHERE TopicID = @TopicId; 
                DELETE FROM TestResults WHERE TopicID = @TopicId;
                DELETE FROM Questions WHERE TopicID = @TopicId;
            ";

            const string deleteTopicSql = "DELETE FROM Topics WHERE TopicID = @TopicId";

            try
            {
                ExecuteSql(deleteRelatedSql, new { TopicId = topicId });
                ExecuteSql(deleteTopicSql, new { TopicId = topicId });
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при каскадном удалении темы: " + ex.Message, ex);
            }
        }

        // =============================================
        // МЕТОДЫ ДЛЯ ЗАКЛАДОК (С ПОДДЕРЖКОЙ КУРСОВ)
        // =============================================
        public static void AddBookmark(int userId, int topicId)
        {
            const string sql = @"
                INSERT INTO Bookmarks (UserID, TopicID, DateAdded) 
                VALUES (@UserID, @TopicID, GETDATE())";
            ExecuteSql(sql, new { UserID = userId, TopicID = topicId });
        }

        public static List<Bookmark> GetBookmarksByUserId(int userId, int? courseId = null)
        {
            if (courseId == null) courseId = CurrentCourseId;

            const string sql = @"
                SELECT 
                    b.BookmarkID, b.UserID, b.TopicID, b.DateAdded,
                    t.Title AS TopicTitle
                FROM Bookmarks b
                JOIN Topics t ON b.TopicID = t.TopicID
                WHERE b.UserID = @UserID AND t.CourseID = @CourseId
                ORDER BY b.DateAdded DESC";

            return LoadData<Bookmark>(sql, new { UserID = userId, CourseId = courseId });
        }

        public static void RemoveBookmark(int userId, int topicId)
        {
            const string sql = "DELETE FROM Bookmarks WHERE UserID = @UserID AND TopicID = @TopicID";
            ExecuteSql(sql, new { UserID = userId, TopicID = topicId });
        }

        // =============================================
        // МЕТОДЫ ДЛЯ ТЕСТОВ (С ПОДДЕРЖКОЙ КУРСОВ)
        // =============================================
        public static int GetQuestionCountByTopicId(int? topicId, int? courseId = null)
        {
            if (courseId == null) courseId = CurrentCourseId;

            using (IDbConnection connection = new SqlConnection(ConnectionString))
            {
                if (topicId == null)
                {
                    const string sql = "SELECT COUNT(1) FROM Questions WHERE CourseID = @CourseId";
                    return connection.QueryFirstOrDefault<int>(sql, new { CourseId = courseId });
                }
                else
                {
                    const string sql = "SELECT COUNT(1) FROM Questions WHERE TopicID = @TopicID AND CourseID = @CourseId";
                    return connection.QueryFirstOrDefault<int>(sql, new { TopicID = topicId, CourseId = courseId });
                }
            }
        }

        public static int GetTotalQuestionCount(int? courseId = null)
        {
            return GetQuestionCountByTopicId(null, courseId);
        }

        public static List<Question> GetQuestionsByTopicId(int topicId, int? courseId = null)
        {
            if (courseId == null) courseId = CurrentCourseId;

            string sql = $@"
                SELECT QuestionID, TopicID, Text, CourseID, QuestionType, Difficulty, Explanation 
                FROM Questions 
                WHERE TopicID = @TopicId AND CourseID = @CourseId;
                
                SELECT AnswerID, QuestionID, Text, IsCorrect 
                FROM Answers 
                WHERE QuestionID IN (SELECT QuestionID FROM Questions WHERE TopicID = @TopicId AND CourseID = @CourseId);";

            using (var connection = new SqlConnection(ConnectionString))
            using (var multi = connection.QueryMultiple(sql, new { TopicId = topicId, CourseId = courseId }))
            {
                var questions = multi.Read<Question>().ToList();
                var answers = multi.Read<Answer>().ToList();

                foreach (var q in questions)
                {
                    q.Answers = answers.Where(a => a.QuestionID == q.QuestionID).ToList();
                }

                return questions;
            }
        }

        public static void SaveTestResult(int userId, int? topicId, int totalQuestions, int correctAnswers, int? courseId = null)
        {
            if (courseId == null) courseId = CurrentCourseId;

            const string sql = @"
                INSERT INTO TestResults (UserID, TopicID, TotalQuestions, CorrectAnswers, TestDate, CourseID) 
                VALUES (@UserID, @TopicID, @TotalQuestions, @CorrectAnswers, GETDATE(), @CourseID)";

            ExecuteSql(sql, new
            {
                UserID = userId,
                TopicID = topicId,
                TotalQuestions = totalQuestions,
                CorrectAnswers = correctAnswers,
                CourseID = courseId
            });
        }

        public static List<TestStatistic> GetTestStatisticsByUserId(int userId, int? courseId = null)
        {
            if (courseId == null) courseId = CurrentCourseId;

            const string sql = @"
                SELECT
                    T.TopicID,
                    T.Title AS TopicTitle,
                    ISNULL(COUNT(TR.UserID), 0) AS Attempts, 
                    ISNULL(CAST(AVG(CAST(TR.CorrectAnswers AS DECIMAL(18, 2)) * 100.0 / TR.TotalQuestions) AS DECIMAL(5, 2)), 0) AS AvgScore,
                    ISNULL(CAST(MAX(CAST(TR.CorrectAnswers AS DECIMAL(18, 2)) * 100.0 / TR.TotalQuestions) AS DECIMAL(5, 2)), 0) AS BestScore,
                    @CourseId AS CourseID
                FROM dbo.Topics T
                LEFT JOIN dbo.TestResults TR ON T.TopicID = TR.TopicID AND TR.UserID = @UserId AND TR.CourseID = @CourseId
                WHERE T.CourseID = @CourseId
                GROUP BY T.TopicID, T.Title
                ORDER BY T.TopicID;";

            return LoadData<TestStatistic>(sql, new { UserId = userId, CourseId = courseId });
        }
        // =============================================
        // МЕТОДЫ ДЛЯ СЛОВАРЯ ТЕРМИНОВ (общие для всех курсов)
        // =============================================

        public static List<GlossaryTerm> GetAllGlossaryTerms(int? courseId = null)
        {
            string sql;
            object parameters = null;

            if (courseId == null)
            {
                // Если курс не указан — загружаем все термины
                sql = @"
            SELECT TermID, Term, Definition, CourseID, TopicID, CreatedAt
            FROM GlossaryTerms
            ORDER BY Term ASC";
            }
            else
            {
                // Если курс указан — загружаем только для этого курса
                sql = @"
            SELECT TermID, Term, Definition, CourseID, TopicID, CreatedAt
            FROM GlossaryTerms
            WHERE CourseID = @CourseId
            ORDER BY Term ASC";
                parameters = new { CourseId = courseId };
            }

            return LoadData<GlossaryTerm>(sql, parameters);
        }

        public static List<GlossaryTerm> SearchGlossaryTerms(string searchText, int? courseId = null)
        {
            string sql;
            object parameters;

            if (courseId == null)
            {
                // Поиск по всем терминам
                sql = @"
            SELECT TermID, Term, Definition, CourseID, TopicID, CreatedAt
            FROM GlossaryTerms
            WHERE (Term LIKE @Search OR Definition LIKE @Search)
            ORDER BY Term ASC";
                parameters = new { Search = $"%{searchText}%" };
            }
            else
            {
                // Поиск только по указанному курсу
                sql = @"
            SELECT TermID, Term, Definition, CourseID, TopicID, CreatedAt
            FROM GlossaryTerms
            WHERE CourseID = @CourseId 
              AND (Term LIKE @Search OR Definition LIKE @Search)
            ORDER BY Term ASC";
                parameters = new
                {
                    CourseId = courseId,
                    Search = $"%{searchText}%"
                };
            }

            return LoadData<GlossaryTerm>(sql, parameters);
        }

        public static int GetGlossaryTermsCount(int? courseId = null)
        {
            string sql;
            object parameters = null;

            if (courseId == null)
            {
                sql = "SELECT COUNT(*) FROM GlossaryTerms";
            }
            else
            {
                sql = "SELECT COUNT(*) FROM GlossaryTerms WHERE CourseID = @CourseId";
                parameters = new { CourseId = courseId };
            }

            using (var connection = new SqlConnection(ConnectionString))
            {
                return connection.QueryFirstOrDefault<int>(sql, parameters);
            }
        }

        // =============================================
        // МЕТОДЫ ДЛЯ ПРИМЕРОВ КОДА
        // =============================================
        public static List<CodeExample> GetCodeExamplesByTopicId(int topicId, int? courseId = null)
        {
            if (courseId == null) courseId = CurrentCourseId;

            const string sql = @"
                SELECT ExampleID, TopicID, Title, HTMLCode, CSSCode, JSCode, CourseID, CreatedAt
                FROM CodeExamples
                WHERE TopicID = @TopicId AND CourseID = @CourseId
                ORDER BY CreatedAt DESC";

            return LoadData<CodeExample>(sql, new { TopicId = topicId, CourseId = courseId });
        }

        // =============================================
        // МЕТОДЫ ДЛЯ ПРОГРЕССА ПОЛЬЗОВАТЕЛЯ
        // =============================================
        public static void UpdateUserProgress(int userId, int topicId, int timeSpentSeconds, int? courseId = null)
        {
            if (courseId == null) courseId = CurrentCourseId;

            const string sql = @"
                IF EXISTS (SELECT 1 FROM UserProgress WHERE UserID = @UserID AND TopicID = @TopicID AND CourseID = @CourseID)
                    UPDATE UserProgress 
                    SET IsRead = 1, LastViewedAt = GETDATE(), TimeSpentSeconds = TimeSpentSeconds + @TimeSpentSeconds
                    WHERE UserID = @UserID AND TopicID = @TopicID AND CourseID = @CourseID
                ELSE
                    INSERT INTO UserProgress (UserID, TopicID, IsRead, LastViewedAt, TimeSpentSeconds, CourseID)
                    VALUES (@UserID, @TopicID, 1, GETDATE(), @TimeSpentSeconds, @CourseID)";

            ExecuteSql(sql, new { UserID = userId, TopicID = topicId, TimeSpentSeconds = timeSpentSeconds, CourseID = courseId });
        }

        public static List<UserProgress> GetUserProgress(int userId, int? courseId = null)
        {
            if (courseId == null) courseId = CurrentCourseId;

            const string sql = @"
        SELECT ProgressID, UserID, up.TopicID, IsRead, LastViewedAt, TimeSpentSeconds, up.CourseID,
               t.Title AS TopicTitle
        FROM UserProgress up
        JOIN Topics t ON up.TopicID = t.TopicID
        WHERE up.UserID = @UserID 
          AND up.CourseID = @CourseId
          AND up.IsRead = 1   -- 👈 ЭТО КЛЮЧЕВОЕ УСЛОВИЕ
        ORDER BY LastViewedAt DESC";

            return LoadData<UserProgress>(sql, new { UserID = userId, CourseId = courseId });
        }

        // =============================================
        // МЕТОДЫ ДЛЯ НАСТРОЕК ПОЛЬЗОВАТЕЛЯ
        // =============================================
        public static UserSetting GetUserSettings(int userId)
        {
            const string sql = @"
                SELECT SettingID, UserID, FontSize, Theme
                FROM UserSettings
                WHERE UserID = @UserID";

            var settings = LoadData<UserSetting>(sql, new { UserID = userId }).FirstOrDefault();

            if (settings == null)
            {
                // Создаем настройки по умолчанию
                const string insertSql = @"
                    INSERT INTO UserSettings (UserID, FontSize, Theme)
                    OUTPUT INSERTED.*
                    VALUES (@UserID, 'Medium', 'Light')";

                using (var connection = new SqlConnection(ConnectionString))
                {
                    settings = connection.QuerySingle<UserSetting>(insertSql, new { UserID = userId });
                }
            }

            return settings;
        }

        public static void SaveUserSettings(int userId, string fontSize, string theme)
        {
            const string sql = @"
                UPDATE UserSettings 
                SET FontSize = @FontSize, Theme = @Theme
                WHERE UserID = @UserID";

            ExecuteSql(sql, new { UserID = userId, FontSize = fontSize, Theme = theme });
        }

        // =============================================
        // МЕТОДЫ ДЛЯ АДМИН-ЛОГОВ
        // =============================================
        public static void LogAdminAction(int adminId, string actionType, int? targetId = null, string details = null)
        {
            const string sql = @"
                INSERT INTO AdminLogs (AdminID, ActionType, TargetID, Details, Timestamp)
                VALUES (@AdminID, @ActionType, @TargetID, @Details, GETDATE())";

            ExecuteSql(sql, new { AdminID = adminId, ActionType = actionType, TargetID = targetId, Details = details });
        }

        // =============================================
        // МЕТОДЫ ДЛЯ ПОЛЬЗОВАТЕЛЕЙ (БЕЗ ИЗМЕНЕНИЙ)
        // =============================================
        private static string HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static bool RegisterUser(string username, string email, string password)
        {
            try
            {
                string hashedPassword = HashPassword(password);
                const string query = @"
                    INSERT INTO dbo.Users (Username, Email, PasswordHash, RoleID, RegistrationDate) 
                    VALUES (@Username, @Email, @PasswordHash, 3, GETDATE())";

                ExecuteSql(query, new
                {
                    Username = username,
                    Email = email,
                    PasswordHash = hashedPassword
                });
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при регистрации: " + ex.Message);
                return false;
            }
        }

        public static bool IsEmailExists(string email)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                string query = "SELECT COUNT(1) FROM Users WHERE Email = @Email";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Email", email);
                    int count = Convert.ToInt32(command.ExecuteScalar());
                    return count > 0;
                }
            }
        }

        public static bool IsUsernameExists(string username)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                string query = "SELECT COUNT(1) FROM Users WHERE Username = @Username";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Username", username);
                    return Convert.ToInt32(command.ExecuteScalar()) > 0;
                }
            }
        }

        public static User AuthenticateUser(string login, string password)
        {
            string hashedPassword = HashPassword(password);

            // ═══ КРИТИЧЕСКИ ВАЖНО: SELECT ДОЛЖЕН ВКЛЮЧАТЬ RoleID ═══
            string sql = @"
        SELECT 
            UserID, 
            Username, 
            Email, 
            PasswordHash, 
            RoleID,
            RegistrationDate
        FROM Users 
        WHERE (Email = @Login OR Username = @Login)
          AND PasswordHash = @PasswordHash
          AND (IsEmailVerified = 1 OR RoleID IN (1, 2))";

            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    var user = connection.QueryFirstOrDefault<User>(sql, new
                    {
                        Login = login,
                        PasswordHash = hashedPassword
                    });

                    if (user != null)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[AuthenticateUser] Loaded: {user.Username}, RoleID={user.RoleID}");

                        UpdateUserStreak(user.UserID);
                    }

                    return user;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthenticateUser] ERROR: {ex.Message}");
                throw;
            }
        }
        public static List<User> GetAllUsers()
        {
            const string sql = @"
                SELECT UserID, Username, Email, PasswordHash, RegistrationDate, RoleID,
                       ISNULL(IsEmailVerified, 0) AS IsEmailVerified
                FROM dbo.Users
                ORDER BY RoleID, Username";
            return LoadData<User>(sql);
        }

        public static void UpdateUser(User user)
        {
            const string sql = @"
                UPDATE dbo.Users 
                SET Username = @Username, Email = @Email, RoleID = @RoleID 
                WHERE UserID = @UserID";
            ExecuteSql(sql, user);
        }

        public static void DeleteUser(int userId)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        connection.Execute("DELETE FROM ReviewLog WHERE UserID = @UserID", new { UserID = userId }, transaction);
                        connection.Execute("DELETE FROM Bookmarks WHERE UserID = @UserID", new { UserID = userId }, transaction);
                        connection.Execute("DELETE FROM TestResults WHERE UserID = @UserID", new { UserID = userId }, transaction);
                        connection.Execute("DELETE FROM UserProgress WHERE UserID = @UserID", new { UserID = userId }, transaction);
                        connection.Execute("DELETE FROM UserSettings WHERE UserID = @UserID", new { UserID = userId }, transaction);
                        connection.Execute("DELETE FROM UserAchievements WHERE UserID = @UserID", new { UserID = userId }, transaction);
                        connection.Execute("DELETE FROM TopicNotes WHERE UserID = @UserID", new { UserID = userId }, transaction);
                        connection.Execute("DELETE FROM EmailVerifications WHERE UserID = @UserID", new { UserID = userId }, transaction);
                        connection.Execute("DELETE FROM GroupMemberships WHERE UserID = @UserID", new { UserID = userId }, transaction);
                        connection.Execute("DELETE FROM dbo.Users WHERE UserID = @UserID", new { UserID = userId }, transaction);

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show("Ошибка при удалении пользователя: " + ex.Message);
                        throw;
                    }
                }
            }
        }

        public static void SaveAllQuestionsForTopic(int topicId, List<Question> questionsToSave)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var existingQuestionIds = connection.Query<int>(
                            "SELECT QuestionID FROM Questions WHERE TopicID = @TopicId",
                            new { TopicId = topicId },
                            transaction)
                            .ToList();

                        var currentQuestionIds = questionsToSave
                            .Where(q => q.QuestionID > 0)
                            .Select(q => q.QuestionID)
                            .ToList();

                        var questionsToDelete = existingQuestionIds.Except(currentQuestionIds).ToList();

                        if (questionsToDelete.Any())
                        {
                            connection.Execute(
                                "DELETE FROM Answers WHERE QuestionID IN @Ids",
                                new { Ids = questionsToDelete },
                                transaction);

                            connection.Execute(
                                "DELETE FROM Questions WHERE QuestionID IN @Ids",
                                new { Ids = questionsToDelete },
                                transaction);
                        }

                        foreach (var q in questionsToSave)
                        {
                            if (q.QuestionID == 0)
                            {
                                const string insertQuestionSql = @"
                                    INSERT INTO Questions (TopicID, Text, CourseID, QuestionType, Difficulty, Explanation) 
                                    OUTPUT INSERTED.QuestionID
                                    VALUES (@TopicID, @Text, @CourseID, @QuestionType, @Difficulty, @Explanation)";

                                q.QuestionID = connection.QuerySingle<int>(
                                    insertQuestionSql,
                                    new { q.TopicID, q.Text, q.CourseID, q.QuestionType, q.Difficulty, q.Explanation },
                                    transaction);
                            }
                            else
                            {
                                const string updateQuestionSql = @"
                                    UPDATE Questions 
                                    SET Text = @Text, QuestionType = @QuestionType, Difficulty = @Difficulty, Explanation = @Explanation 
                                    WHERE QuestionID = @QuestionID";
                                connection.Execute(updateQuestionSql, q, transaction);

                                var currentAnswerIds = q.Answers
                                    .Where(a => a.AnswerID > 0)
                                    .Select(a => a.AnswerID)
                                    .ToList();

                                connection.Execute(
                                    "DELETE FROM Answers WHERE QuestionID = @QuestionID AND AnswerID NOT IN @CurrentAnswerIds",
                                    new { q.QuestionID, CurrentAnswerIds = currentAnswerIds },
                                    transaction);
                            }

                            foreach (var a in q.Answers)
                            {
                                a.QuestionID = q.QuestionID;

                                if (a.AnswerID == 0)
                                {
                                    const string insertAnswerSql = @"
                                        INSERT INTO Answers (QuestionID, Text, IsCorrect) 
                                        OUTPUT INSERTED.AnswerID
                                        VALUES (@QuestionID, @Text, @IsCorrect)";

                                    a.AnswerID = connection.QuerySingle<int>(
                                        insertAnswerSql,
                                        a,
                                        transaction);
                                }
                                else
                                {
                                    const string updateAnswerSql = @"
                                        UPDATE Answers 
                                        SET Text = @Text, IsCorrect = @IsCorrect 
                                        WHERE AnswerID = @AnswerID";
                                    connection.Execute(updateAnswerSql, a, transaction);
                                }
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show("Ошибка транзакции сохранения вопросов: " + ex.Message);
                        throw;
                    }
                }
            }
        }

        // Остальные методы (ShouldBlockTest, HasUserCompletedReviewAfterBlockTrigger, LogTopicReview)
        // оставляем без изменений, только добавляем проверку CourseID если нужно
        private const int MinPassingScorePercent = 50;
        private const int AttemptsThreshold = 3;

        public static bool ShouldBlockTest(int userId, int topicId)
        {
            const string sql = @"
                SELECT TOP (@AttemptsThreshold) 
                    (CAST(CorrectAnswers AS DECIMAL(5, 2)) * 100.0 / TotalQuestions) AS Score
                FROM TestResults 
                WHERE UserID = @UserID AND TopicID = @TopicID
                ORDER BY TestDate DESC";

            var parameters = new
            {
                UserID = userId,
                TopicID = topicId,
                AttemptsThreshold = AttemptsThreshold
            };

            using (IDbConnection connection = new SqlConnection(ConnectionString))
            {
                var recentScores = connection.Query<decimal>(sql, parameters).ToList();

                if (recentScores.Count >= AttemptsThreshold)
                {
                    return recentScores.All(score => score < MinPassingScorePercent);
                }

                return false;
            }
        }

        public static bool HasUserCompletedReviewAfterBlockTrigger(int userId, int topicId)
        {
            const string oldestFailDateSql = @"
                SELECT TestDate
                FROM TestResults
                WHERE UserID = @UserID AND TopicID = @TopicID
                ORDER BY TestDate DESC
                OFFSET @AttemptsThreshold - 1 ROWS FETCH NEXT 1 ROW ONLY";

            const string reviewDateSql = @"
                SELECT MAX(ReviewDate) 
                FROM ReviewLog 
                WHERE UserID = @UserID AND TopicID = @TopicID";

            using (IDbConnection connection = new SqlConnection(ConnectionString))
            {
                var recentScores = connection.Query<decimal>(
                    "SELECT TOP 3 (CAST(CorrectAnswers AS DECIMAL(5, 2)) * 100.0 / TotalQuestions) AS Score FROM TestResults WHERE UserID = @UserID AND TopicID = @TopicID ORDER BY TestDate DESC",
                    new { UserID = userId, TopicID = topicId }
                ).ToList();

                if (recentScores.Count < AttemptsThreshold || recentScores.Any(score => score >= MinPassingScorePercent))
                {
                    return true;
                }

                var blockTriggerDate = connection.QueryFirstOrDefault<DateTime?>(
                    oldestFailDateSql,
                    new { UserID = userId, TopicID = topicId, AttemptsThreshold = AttemptsThreshold });

                if (blockTriggerDate == null) return true;

                var lastReviewDate = connection.QueryFirstOrDefault<DateTime?>(reviewDateSql, new { UserID = userId, TopicID = topicId });

                return lastReviewDate != null && lastReviewDate > blockTriggerDate;
            }
        }

        public static void LogTopicReview(int userId, int topicId)
        {
            const string sql = @"
                INSERT INTO ReviewLog (UserID, TopicID, ReviewDate) 
                VALUES (@UserID, @TopicID, GETDATE())";

            var parameters = new
            {
                UserID = userId,
                TopicID = topicId
            };

            using (IDbConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Execute(sql, parameters);
            }
        }
        // =============================================
        // МЕТОДЫ ДЛЯ ЗАМЕТОК
        // =============================================

        public static TopicNote GetTopicNote(int userId, int topicId)
        {
            const string sql = @"
        SELECT n.*, t.Title AS TopicTitle
        FROM TopicNotes n
        JOIN Topics t ON n.TopicID = t.TopicID
        WHERE n.UserID = @UserID AND n.TopicID = @TopicID";

            return LoadData<TopicNote>(sql, new { UserID = userId, TopicID = topicId }).FirstOrDefault();
        }

        public static void SaveTopicNote(int userId, int topicId, string noteText)
        {
            const string sql = @"
        IF EXISTS (SELECT 1 FROM TopicNotes WHERE UserID = @UserID AND TopicID = @TopicID)
            UPDATE TopicNotes 
            SET NoteText = @NoteText, UpdatedAt = GETDATE()
            WHERE UserID = @UserID AND TopicID = @TopicID
        ELSE
            INSERT INTO TopicNotes (UserID, TopicID, NoteText, CreatedAt)
            VALUES (@UserID, @TopicID, @NoteText, GETDATE())";

            ExecuteSql(sql, new { UserID = userId, TopicID = topicId, NoteText = noteText });
        }

        public static List<TopicNote> GetAllUserNotes(int userId)
        {
            const string sql = @"
        SELECT n.*, t.Title AS TopicTitle
        FROM TopicNotes n
        JOIN Topics t ON n.TopicID = t.TopicID
        WHERE n.UserID = @UserID
        ORDER BY n.UpdatedAt DESC, n.CreatedAt DESC";

            return LoadData<TopicNote>(sql, new { UserID = userId });
        }

        public static void DeleteTopicNote(int userId, int topicId)
        {
            const string sql = "DELETE FROM TopicNotes WHERE UserID = @UserID AND TopicID = @TopicID";
            ExecuteSql(sql, new { UserID = userId, TopicID = topicId });
        }
        // =============================================
        // ДОСТИЖЕНИЯ
        // =============================================

        public static void InitializeAchievements()
        {
            // Добавляем колонки стрика если их нет (безопасно при повторных запусках)
            const string alterSql = @"
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'CurrentStreak')
            ALTER TABLE Users ADD CurrentStreak INT NOT NULL DEFAULT 0;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'MaxStreak')
            ALTER TABLE Users ADD MaxStreak INT NOT NULL DEFAULT 0;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'LastLoginDate')
            ALTER TABLE Users ADD LastLoginDate DATE NULL;";
            try { ExecuteSql(alterSql); } catch { }

            // Таблица Achievements уже заполнена в БД вручную — не трогаем её из кода
        }

        public static List<UserAchievement> GetUserAchievements(int userId)
        {
            const string sql = @"
        SELECT ua.*, a.Title, a.Description, a.IconEmoji, a.XP
        FROM UserAchievements ua
        JOIN Achievements a ON ua.AchievementID = a.AchievementID
        WHERE ua.UserID = @UserID
        ORDER BY ua.EarnedDate DESC";

            return LoadData<UserAchievement>(sql, new { UserID = userId });
        }

        public static void AwardAchievement(int userId, int achievementId)
        {
            const string sql = @"
        IF NOT EXISTS (SELECT 1 FROM UserAchievements WHERE UserID = @UserID AND AchievementID = @AchievementID)
            INSERT INTO UserAchievements (UserID, AchievementID, EarnedDate)
            VALUES (@UserID, @AchievementID, GETDATE())";

            ExecuteSql(sql, new { UserID = userId, AchievementID = achievementId });
        }
        public static List<LeaderboardEntry> GetLeaderboard(int topCount = 10)
        {
            const string sql = @"
        SELECT TOP (@TopCount)
            u.UserID,
            u.Username,
            ISNULL(SUM(a.XP), 0) AS TotalXP,
            COUNT(ua.UserAchievementID) AS AchievementsCount
        FROM Users u
        LEFT JOIN UserAchievements ua ON u.UserID = ua.UserID
        LEFT JOIN Achievements a ON ua.AchievementID = a.AchievementID
        GROUP BY u.UserID, u.Username
        ORDER BY TotalXP DESC";

            var list = LoadData<LeaderboardEntry>(sql, new { TopCount = topCount });

            // Добавляем ранги
            int rank = 1;
            foreach (var entry in list)
            {
                entry.Rank = rank++;
            }

            return list;
        }

        public static List<AchievementWithStatus> GetAllAchievementsWithStatus(int userId)
        {
            // Все достижения
            var allAchievements = LoadData<Achievement>("SELECT * FROM Achievements ORDER BY AchievementID");

            // Достижения пользователя
            var userAchievements = GetUserAchievements(userId);

            // Статистика тестов
            var testStats = GetTestStatisticsByUserId(userId);
            int testsPassed = testStats?.Sum(t => t.Attempts) ?? 0;

            var bookmarks = GetAllBookmarksByUserId(userId);
            int bookmarksCount = bookmarks?.Count ?? 0;

            var progress = GetAllUserProgress(userId);
            int topicsRead = progress?.Count ?? 0;

            // Количество созданных заметок
            int notesCreated = 0;
            using (var conn = new SqlConnection(ConnectionString))
            {
                notesCreated = conn.QueryFirstOrDefault<int>(
                    "SELECT COUNT(*) FROM TopicNotes WHERE UserID = @UserID",
                    new { UserID = userId });
            }

            // Количество открытий глоссария (считаем из GlossaryOpenLog если есть, иначе из UserSettings)
            int glossaryOpened = 0;
            using (var conn = new SqlConnection(ConnectionString))
            {
                try
                {
                    glossaryOpened = conn.QueryFirstOrDefault<int>(
                        "SELECT ISNULL(GlossaryOpenCount, 0) FROM UserSettings WHERE UserID = @UserID",
                        new { UserID = userId });
                }
                catch (Exception exG)
                {
                    System.Diagnostics.Debug.WriteLine($"[GetAchievements] glossaryOpened: {exG.Message}");
                    glossaryOpened = 0;
                }
            }

            // 👇 ИСПРАВЛЕНО: считаем УНИКАЛЬНЫЕ темы с идеальным результатом
            int perfectTests = 0;
            using (var connection = new SqlConnection(ConnectionString))
            {
                const string sql = @"
            SELECT COUNT(DISTINCT TopicID) 
            FROM TestResults 
            WHERE UserID = @UserID 
              AND CorrectAnswers = TotalQuestions";  // Только идеальные тесты, уникальные темы
                perfectTests = connection.QueryFirstOrDefault<int>(sql, new { UserID = userId });
            }

            // Streak
            int currentStreak = 0;
            using (var connection = new SqlConnection(ConnectionString))
            {
                currentStreak = connection.QueryFirstOrDefault<int>(
                    "SELECT CurrentStreak FROM Users WHERE UserID = @UserID",
                    new { UserID = userId });
            }

            var result = new List<AchievementWithStatus>();

            foreach (var ach in allAchievements)
            {
                bool isUnlocked = userAchievements.Any(ua => ua.AchievementID == ach.AchievementID);

                var status = new AchievementWithStatus
                {
                    AchievementID = ach.AchievementID,
                    Title = ach.Title,
                    Description = ach.Description,
                    IconEmoji = ach.IconEmoji,
                    IconPath = ach.IconPath,
                    XP = ach.XP,
                    ConditionType = ach.ConditionType,
                    ConditionValue = ach.ConditionValue,
                    IsUnlocked = isUnlocked,
                    UnlockedDate = userAchievements.FirstOrDefault(ua => ua.AchievementID == ach.AchievementID)?.EarnedDate
                };

                if (isUnlocked)
                {
                    status.CurrentProgress = ach.ConditionValue;
                }
                else
                {
                    switch (ach.ConditionType)
                    {
                        case "tests_taken":        // пройденные тесты
                            status.CurrentProgress = Math.Min(testsPassed, ach.ConditionValue);
                            break;
                        case "bookmarks":
                            status.CurrentProgress = Math.Min(bookmarksCount, ach.ConditionValue);
                            break;
                        case "topics_read":
                            status.CurrentProgress = Math.Min(topicsRead, ach.ConditionValue);
                            break;
                        case "perfect_score":      // 100% в тесте
                            status.CurrentProgress = Math.Min(perfectTests, ach.ConditionValue);
                            break;
                        case "perfect_streak":     // 100% N раз подряд
                            status.CurrentProgress = Math.Min(perfectTests, ach.ConditionValue);
                            break;
                        case "streak":
                            status.CurrentProgress = Math.Min(currentStreak, ach.ConditionValue);
                            break;
                        case "notes":              // заметки к темам
                            status.CurrentProgress = Math.Min(notesCreated, ach.ConditionValue);
                            break;
                        case "glossary_views":     // просмотры глоссария
                            status.CurrentProgress = Math.Min(glossaryOpened, ach.ConditionValue);
                            break;
                        case "course_complete":
                            status.CurrentProgress = Math.Min(GetCompletedCoursesCount(userId), ach.ConditionValue);
                            break;
                        case "courses_joined":
                            status.CurrentProgress = Math.Min(GetCoursesJoinedCount(userId), ach.ConditionValue);
                            break;
                        case "code_views":
                            status.CurrentProgress = Math.Min(GetCodeViewsCount(userId), ach.ConditionValue);
                            break;
                        case "night_session":
                            status.CurrentProgress = Math.Min(GetNightSessionsCount(userId), ach.ConditionValue);
                            break;
                        default:
                            status.CurrentProgress = 0;
                            break;
                    }
                }

                result.Add(status);
            }

            return result.OrderByDescending(a => a.IsUnlocked)
                         .ThenBy(a => a.ConditionValue)
                         .ToList();
        }

        // ── Аватар пользователя ─────────────────────────────────────
        public static string GetUserAvatar(int userId)
        {
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    // Колонка может отсутствовать в старых БД — используем ISNULL
                    return conn.QueryFirstOrDefault<string>(
                        "SELECT ISNULL(Avatar,'') FROM UserSettings WHERE UserID=@UserID",
                        new { UserID = userId }) ?? "";
                }
            }
            catch { return ""; }
        }

        public static void SaveUserAvatar(int userId, string avatar)
        {
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    // Убедимся что колонка есть
                    conn.Execute(@"
                        IF NOT EXISTS (SELECT 1 FROM sys.columns
                                       WHERE object_id=OBJECT_ID('UserSettings') AND name='Avatar')
                            ALTER TABLE UserSettings ADD Avatar NVARCHAR(10) NOT NULL DEFAULT '';");

                    int updated = conn.Execute(
                        "UPDATE UserSettings SET Avatar=@Avatar WHERE UserID=@UserID",
                        new { Avatar = avatar, UserID = userId });

                    if (updated == 0)
                    {
                        // Записи ещё нет — создаём
                        conn.Execute(
                            "INSERT INTO UserSettings (UserID, FontSize, Theme, GlossaryOpenCount, Avatar) VALUES (@UserID,'Medium','Light',0,@Avatar)",
                            new { UserID = userId, Avatar = avatar });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SaveUserAvatar] {ex.Message}");
            }
        }

        // Увеличиваем счётчик открытий глоссария (колонка создаётся автоматически)
        public static void IncrementGlossaryOpenCount(int userId)
        {
            try
            {
                // Добавляем колонку если её нет
                const string alterSql = @"
                    IF NOT EXISTS (SELECT 1 FROM sys.columns 
                                  WHERE object_id = OBJECT_ID('UserSettings') AND name = 'GlossaryOpenCount')
                        ALTER TABLE UserSettings ADD GlossaryOpenCount INT NOT NULL DEFAULT 0;";
                ExecuteSql(alterSql);

                // Увеличиваем или создаём запись настроек
                const string sql = @"
                    IF EXISTS (SELECT 1 FROM UserSettings WHERE UserID = @UserID)
                    BEGIN
                        UPDATE UserSettings SET GlossaryOpenCount = ISNULL(GlossaryOpenCount, 0) + 1
                        WHERE UserID = @UserID
                    END
                    ELSE
                    BEGIN
                        INSERT INTO UserSettings (UserID, FontSize, Theme, GlossaryOpenCount)
                        VALUES (@UserID, 'Medium', 'Light', 1)
                    END";
                ExecuteSql(sql, new { UserID = userId });
            }
            catch { }
        }

        public static void UpdateUserStreak(int userId)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                // Получаем дату последнего входа
                var lastLogin = connection.QueryFirstOrDefault<DateTime?>(
                    "SELECT LastLoginDate FROM Users WHERE UserID = @UserID",
                    new { UserID = userId });

                DateTime today = DateTime.Today;

                if (lastLogin == null)
                {
                    // Пользователь заходит впервые
                    connection.Execute(
                        "UPDATE Users SET LastLoginDate = @Today, CurrentStreak = 1, MaxStreak = 1 WHERE UserID = @UserID",
                        new { UserID = userId, Today = today });
                }
                else
                {
                    var last = lastLogin.Value.Date;
                    var daysDiff = (today - last).Days;

                    if (daysDiff == 1)
                    {
                        // Зашел на следующий день — увеличиваем streak
                        connection.Execute(@"
                    UPDATE Users SET 
                        LastLoginDate = @Today,
                        CurrentStreak = CurrentStreak + 1,
                        MaxStreak = CASE WHEN CurrentStreak + 1 > MaxStreak THEN CurrentStreak + 1 ELSE MaxStreak END
                    WHERE UserID = @UserID",
                            new { UserID = userId, Today = today });
                    }
                    else if (daysDiff > 1)
                    {
                        // Пропустил день — сбрасываем streak
                        connection.Execute(
                            "UPDATE Users SET LastLoginDate = @Today, CurrentStreak = 1 WHERE UserID = @UserID",
                            new { UserID = userId, Today = today });
                    }
                    // Если daysDiff == 0 (зашел дважды в день) — ничего не делаем
                }
            }
        }

        // =============================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ДЛЯ ДОСТИЖЕНИЙ
        // =============================================

        public static int GetCoursesJoinedCount(int userId)
        {
            try { using (var c = new SqlConnection(ConnectionString)) return c.QueryFirstOrDefault<int>("SELECT COUNT(DISTINCT CourseID) FROM UserProgress WHERE UserID = @UserID AND IsRead = 1", new { UserID = userId }); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[GetCoursesJoinedCount] {ex.Message}"); return 0; }
        }

        public static int GetCompletedCoursesCount(int userId)
        {
            const string sql = @"SELECT COUNT(*) FROM (SELECT t.CourseID, COUNT(t.TopicID) AS TotalLeafs, SUM(CASE WHEN up.IsRead = 1 THEN 1 ELSE 0 END) AS ReadLeafs FROM Topics t LEFT JOIN UserProgress up ON up.TopicID = t.TopicID AND up.UserID = @UserID WHERE NOT EXISTS (SELECT 1 FROM Topics child WHERE child.ParentTopicID = t.TopicID) GROUP BY t.CourseID HAVING COUNT(t.TopicID) > 0 AND COUNT(t.TopicID) = SUM(CASE WHEN up.IsRead = 1 THEN 1 ELSE 0 END)) AS c";
            try { using (var c = new SqlConnection(ConnectionString)) return c.QueryFirstOrDefault<int>(sql, new { UserID = userId }); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[GetCompletedCoursesCount] {ex.Message}"); return 0; }
        }

        public static int GetCodeViewsCount(int userId)
        {
            const string sql = @"SELECT COUNT(*) FROM UserProgress up JOIN Topics t ON up.TopicID = t.TopicID WHERE up.UserID = @UserID AND up.IsRead = 1 AND (t.Content LIKE '%<pre%' OR t.Content LIKE '%<code%')";
            try { using (var c = new SqlConnection(ConnectionString)) return c.QueryFirstOrDefault<int>(sql, new { UserID = userId }); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[GetCodeViewsCount] {ex.Message}"); return 0; }
        }

        public static int GetNightSessionsCount(int userId)
        {
            const string sql = @"SELECT COUNT(DISTINCT CAST(LastViewedAt AS DATE)) FROM UserProgress WHERE UserID = @UserID AND IsRead = 1 AND (DATEPART(HOUR, LastViewedAt) >= 23 OR DATEPART(HOUR, LastViewedAt) < 6)";
            try { using (var c = new SqlConnection(ConnectionString)) return c.QueryFirstOrDefault<int>(sql, new { UserID = userId }); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[GetNightSessionsCount] {ex.Message}"); return 0; }
        }

        // =============================================
        // МЕТОДЫ ВЕРИФИКАЦИИ EMAIL
        // =============================================

        public static int GetUserIdByEmail(string email)
        {
            try { using (var c = new SqlConnection(ConnectionString)) return c.QueryFirstOrDefault<int>("SELECT UserID FROM Users WHERE Email = @Email", new { Email = email }); }
            catch { return 0; }
        }

        public static void DeleteUnverifiedUser(int userId)
        {
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Execute("DELETE FROM EmailVerifications WHERE UserID = @ID", new { ID = userId });
                    conn.Execute("DELETE FROM Users WHERE UserID = @ID AND IsEmailVerified = 0", new { ID = userId });
                }
                System.Diagnostics.Debug.WriteLine($"[DeleteUnverifiedUser] UserID={userId} удалён");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DeleteUnverifiedUser] ОШИБКА: {ex.Message}"); }
        }

        public static void SaveVerificationCode(int userId, string email, string code, DateTime expiresAt)
        {
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Execute("UPDATE EmailVerifications SET IsUsed = 1 WHERE UserID = @UserID AND IsUsed = 0", new { UserID = userId });
                    conn.Execute("INSERT INTO EmailVerifications (UserID, Email, Code, ExpiresAt) VALUES (@UserID, @Email, @Code, @ExpiresAt)",
                        new { UserID = userId, Email = email, Code = code, ExpiresAt = expiresAt });
                }
                System.Diagnostics.Debug.WriteLine($"[SaveVerificationCode] Код сохранён для UserID={userId}");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SaveVerificationCode] ОШИБКА: {ex.Message}"); }
        }

        public static bool VerifyEmailCode(int userId, string email, string code)
        {
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    var verif = conn.QueryFirstOrDefault<dynamic>(@"
                        SELECT TOP 1 VerificationID FROM EmailVerifications
                        WHERE UserID = @UserID AND Email = @Email AND Code = @Code
                          AND IsUsed = 0 AND ExpiresAt > GETDATE()
                        ORDER BY CreatedAt DESC",
                        new { UserID = userId, Email = email, Code = code });

                    if (verif == null) return false;

                    conn.Execute("UPDATE EmailVerifications SET IsUsed = 1 WHERE VerificationID = @ID", new { ID = verif.VerificationID });
                    conn.Execute("UPDATE Users SET IsEmailVerified = 1 WHERE UserID = @UserID", new { UserID = userId });
                    System.Diagnostics.Debug.WriteLine($"[VerifyEmailCode] UserID={userId} верифицирован!");
                    return true;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[VerifyEmailCode] ОШИБКА: {ex.Message}"); return false; }
        }

        public static List<Bookmark> GetAllBookmarksByUserId(int userId)
        {
            const string sql = @"
        SELECT 
            b.BookmarkID, b.UserID, b.TopicID, b.DateAdded,
            t.Title AS TopicTitle
        FROM Bookmarks b
        JOIN Topics t ON b.TopicID = t.TopicID
        WHERE b.UserID = @UserID
        ORDER BY b.DateAdded DESC";

            return LoadData<Bookmark>(sql, new { UserID = userId });
        }
        public static List<UserProgress> GetAllUserProgress(int userId)
        {
            const string sql = @"
        SELECT ProgressID, UserID, up.TopicID, IsRead, LastViewedAt, TimeSpentSeconds, up.CourseID,
               t.Title AS TopicTitle
        FROM UserProgress up
        JOIN Topics t ON up.TopicID = t.TopicID
        WHERE up.UserID = @UserID AND up.IsRead = 1
        ORDER BY LastViewedAt DESC";

            return LoadData<UserProgress>(sql, new { UserID = userId });
        }
        public static List<TestStatistic> GetAllTestStatisticsByUserId(int userId)
        {
            const string sql = @"
        SELECT
            T.TopicID,
            T.Title AS TopicTitle,
            ISNULL(COUNT(TR.UserID), 0) AS Attempts, 
            ISNULL(CAST(AVG(CAST(TR.CorrectAnswers AS DECIMAL(18, 2)) * 100.0 / TR.TotalQuestions) AS DECIMAL(5, 2)), 0) AS AvgScore,
            ISNULL(CAST(MAX(CAST(TR.CorrectAnswers AS DECIMAL(18, 2)) * 100.0 / TR.TotalQuestions) AS DECIMAL(5, 2)), 0) AS BestScore
        FROM dbo.Topics T
        LEFT JOIN dbo.TestResults TR ON T.TopicID = TR.TopicID AND TR.UserID = @UserId
        GROUP BY T.TopicID, T.Title
        ORDER BY T.TopicID;";

            return LoadData<TestStatistic>(sql, new { UserId = userId });
        }

        public static List<Role> GetAllRoles()
        {
            const string sql = "SELECT RoleID, RoleName FROM Roles ORDER BY RoleID";
            return LoadData<Role>(sql);
        }

        public static void UpdateUserRole(int userId, int newRoleId)
        {
            const string sql = "UPDATE Users SET RoleID = @RoleID WHERE UserID = @UserID";
            ExecuteSql(sql, new { UserID = userId, RoleID = newRoleId });
        }

        public static bool IsUserAdmin(int userId)
        {
            const string sql = "SELECT RoleID FROM Users WHERE UserID = @UserID";
            using (var connection = new SqlConnection(ConnectionString))
            {
                var roleId = connection.QueryFirstOrDefault<int?>(sql, new { UserID = userId });
                return roleId == 1;
            }
        }



        // =============================================
        // МЕТОДЫ ДЛЯ РАБОТЫ С ГРУППАМИ
        // =============================================

        /// <summary>
        /// Создать новую группу
        /// </summary>
        public static int CreateGroup(string groupName, string description, int? courseId = null)
        {
            const string sql = @"
        INSERT INTO Groups (GroupName, Description, CourseID, CreatedAt)
        OUTPUT INSERTED.GroupID
        VALUES (@GroupName, @Description, @CourseID, GETDATE())";

            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    return connection.QuerySingle<int>(sql, new
                    {
                        GroupName = groupName,
                        Description = description ?? "",
                        CourseID = courseId
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при создании группы: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Обновить информацию о группе
        /// </summary>
        public static void UpdateGroup(int groupId, string groupName, string description, int? courseId)
        {
            const string sql = @"
        UPDATE Groups 
        SET GroupName = @GroupName, 
            Description = @Description, 
            CourseID = @CourseID
        WHERE GroupID = @GroupID";

            try
            {
                ExecuteSql(sql, new
                {
                    GroupID = groupId,
                    GroupName = groupName,
                    Description = description,
                    CourseID = courseId
                });
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при обновлении группы: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Получить все группы
        /// </summary>
        public static List<Group> GetAllGroups()
        {
            const string sql = @"
        SELECT 
            g.GroupID, 
            g.GroupName, 
            g.Description, 
            g.CourseID, 
            g.CreatedAt,
            c.CourseName,
            COUNT(gm.UserID) AS StudentCount
        FROM Groups g
        LEFT JOIN GroupMemberships gm ON g.GroupID = gm.GroupID
        LEFT JOIN Courses c ON g.CourseID = c.CourseID
        GROUP BY g.GroupID, g.GroupName, g.Description, g.CourseID, g.CreatedAt, c.CourseName
        ORDER BY g.GroupName";
            try
            {
                return LoadData<Group>(sql);
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при загрузке групп: " + ex.Message, ex);
            }
        }

        /// Получить группы для конкретного курса
        /// </summary>
        public static List<Group> GetGroupsByCourse(int courseId)
        {
            const string sql = @"
        SELECT 
            g.GroupID, 
            g.GroupName, 
            g.Description, 
            g.CourseID, 
            g.CreatedAt,
            c.CourseName
        FROM Groups g
        LEFT JOIN Courses c ON g.CourseID = c.CourseID
        WHERE g.CourseID = @CourseId
        ORDER BY g.GroupName";

            try
            {
                return LoadData<Group>(sql, new { CourseId = courseId });
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при загрузке групп по курсу: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Получить группу по ID
        /// </summary>
        public static Group GetGroupById(int groupId)
        {
            const string sql = @"
        SELECT 
            g.GroupID, 
            g.GroupName, 
            g.Description, 
            g.CourseID, 
            g.CreatedAt,
            c.CourseName
        FROM Groups g
        LEFT JOIN Courses c ON g.CourseID = c.CourseID
        WHERE g.GroupID = @GroupID";

            try
            {
                return LoadData<Group>(sql, new { GroupID = groupId }).FirstOrDefault();
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при загрузке группы: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Добавить студента в группу
        /// </summary>
        public static void AddStudentToGroup(int groupId, int userId)
        {
            const string sql = @"
        IF NOT EXISTS (SELECT 1 FROM GroupMemberships WHERE GroupID = @GroupID AND UserID = @UserID)
            INSERT INTO GroupMemberships (GroupID, UserID, AddedAt)
            VALUES (@GroupID, @UserID, GETDATE())";

            try
            {
                ExecuteSql(sql, new { GroupID = groupId, UserID = userId });
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при добавлении студента в группу: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Добавить нескольких студентов в группу
        /// </summary>
        public static void AddStudentsToGroup(int groupId, List<int> userIds)
        {
            if (userIds == null || !userIds.Any())
                return;

            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var userId in userIds)
                        {
                            const string sql = @"
                        IF NOT EXISTS (SELECT 1 FROM GroupMemberships WHERE GroupID = @GroupID AND UserID = @UserID)
                            INSERT INTO GroupMemberships (GroupID, UserID, AddedAt)
                            VALUES (@GroupID, @UserID, GETDATE())";

                            connection.Execute(sql, new { GroupID = groupId, UserID = userId }, transaction);
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Удалить студента из группы
        /// </summary>
        public static void RemoveStudentFromGroup(int groupId, int userId)
        {
            const string sql = "DELETE FROM GroupMemberships WHERE GroupID = @GroupID AND UserID = @UserID";

            try
            {
                ExecuteSql(sql, new { GroupID = groupId, UserID = userId });
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при удалении студента из группы: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Получить всех студентов группы (только студентов, RoleID=3)
        /// </summary>
        public static List<User> GetGroupStudents(int groupId)
        {
            const string sql = @"
        SELECT 
            u.UserID, 
            u.Username, 
            u.Email, 
            u.RegistrationDate, 
            u.RoleID
        FROM Users u
        JOIN GroupMemberships gm ON u.UserID = gm.UserID
        WHERE gm.GroupID = @GroupID AND u.RoleID = 3
        ORDER BY u.Username";

            try
            {
                return LoadData<User>(sql, new { GroupID = groupId });
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при загрузке студентов группы: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Получить группы студента
        /// </summary>
        public static List<Group> GetStudentGroups(int userId)
        {
            const string sql = @"
        SELECT 
            g.GroupID, 
            g.GroupName, 
            g.Description, 
            g.CourseID, 
            g.CreatedAt,
            c.CourseName,
            (SELECT COUNT(*) FROM GroupMemberships WHERE GroupID = g.GroupID) as StudentCount
        FROM Groups g
        JOIN GroupMemberships gm ON g.GroupID = gm.GroupID
        LEFT JOIN Courses c ON g.CourseID = c.CourseID
        WHERE gm.UserID = @UserID
        ORDER BY g.GroupName";

            try
            {
                return LoadData<Group>(sql, new { UserID = userId });
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при загрузке групп студента: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Назначить курс группе
        /// </summary>
        public static void AssignCourseToGroup(int groupId, int courseId)
        {
            const string sql = "UPDATE Groups SET CourseID = @CourseID WHERE GroupID = @GroupID";

            try
            {
                ExecuteSql(sql, new { GroupID = groupId, CourseID = courseId });
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при назначении курса группе: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Удалить группу (каскадно удаляются все связи)
        /// </summary>
        public static void DeleteGroup(int groupId)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Сначала удаляем все связи
                        connection.Execute(
                            "DELETE FROM GroupMemberships WHERE GroupID = @GroupID",
                            new { GroupID = groupId },
                            transaction);

                        // Потом саму группу
                        connection.Execute(
                            "DELETE FROM Groups WHERE GroupID = @GroupID",
                            new { GroupID = groupId },
                            transaction);

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw new Exception("Ошибка при удалении группы");
                    }
                }
            }
        }

        /// <summary>
        /// Получить всех студентов (для выбора при создании группы)
        /// </summary>
        public static List<User> GetAllStudents()
        {
            const string sql = @"
        SELECT 
            UserID, 
            Username, 
            Email, 
            RegistrationDate, 
            RoleID
        FROM Users
        WHERE RoleID = 3
        ORDER BY Username";

            try
            {
                return LoadData<User>(sql);
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при загрузке студентов: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Получить подробную статистику по группе
        /// </summary>
        public static GroupStatistics GetGroupStatistics(int groupId, int courseId)
        {
            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    // Получаем информацию о группе
                    var group = GetGroupById(groupId);
                    if (group == null)
                        return null;

                    // Получаем ID студентов группы
                    var studentIds = connection.Query<int>(
                        "SELECT UserID FROM GroupMemberships WHERE GroupID = @GroupID",
                        new { GroupID = groupId }).ToList();

                    var stats = new GroupStatistics
                    {
                        GroupID = groupId,
                        GroupName = group.GroupName,
                        StudentCount = studentIds.Count
                    };

                    if (studentIds.Any())
                    {
                        // Общая статистика по группе
                        var summary = connection.QueryFirstOrDefault<dynamic>(@"
                    SELECT 
                        ISNULL(AVG(CAST(tr.CorrectAnswers AS FLOAT) * 100.0 / tr.TotalQuestions), 0) AS AvgScore,
                        COUNT(tr.ResultID) AS TotalAttempts,
                        COUNT(DISTINCT tr.TopicID) AS CompletedTopics,
                        SUM(CASE WHEN tr.CorrectAnswers = tr.TotalQuestions THEN 1 ELSE 0 END) AS PerfectTests
                    FROM TestResults tr
                    WHERE tr.UserID IN @StudentIDs AND tr.CourseID = @CourseId",
                            new { StudentIDs = studentIds, CourseId = courseId });

                        if (summary != null)
                        {
                            stats.AvgScore = summary.AvgScore;
                            stats.TotalAttempts = summary.TotalAttempts;
                            stats.CompletedTopics = summary.CompletedTopics;
                            stats.PerfectTests = summary.PerfectTests ?? 0;
                        }

                        // Статистика по темам
                        stats.TopicStats = connection.Query<TopicStat>(@"
                    SELECT 
                        t.Title AS TopicTitle,
                        ISNULL(AVG(CAST(tr.CorrectAnswers AS FLOAT) * 100.0 / tr.TotalQuestions), 0) AS GroupAvgScore,
                        ISNULL((
                            SELECT AVG(CAST(tr2.CorrectAnswers AS FLOAT) * 100.0 / tr2.TotalQuestions)
                            FROM TestResults tr2
                            WHERE tr2.TopicID = t.TopicID AND tr2.CourseID = @CourseId
                        ), 0) AS OverallAvgScore
                    FROM Topics t
                    LEFT JOIN TestResults tr ON t.TopicID = tr.TopicID 
                        AND tr.UserID IN @StudentIDs AND tr.CourseID = @CourseId
                    WHERE t.CourseID = @CourseId
                    GROUP BY t.TopicID, t.Title, t.OrderIndex
                    ORDER BY t.OrderIndex",
                            new { StudentIDs = studentIds, CourseId = courseId }).ToList();

                        // Лучшие студенты группы
                        stats.TopStudents = connection.Query<StudentStat>(@"
                    SELECT TOP 5
                        u.UserID,
                        u.Username,
                        ISNULL(AVG(CAST(tr.CorrectAnswers AS FLOAT) * 100.0 / tr.TotalQuestions), 0) AS AvgScore,
                        COUNT(tr.ResultID) AS TotalAttempts
                    FROM Users u
                    LEFT JOIN TestResults tr ON u.UserID = tr.UserID AND tr.CourseID = @CourseId
                    WHERE u.UserID IN @StudentIDs
                    GROUP BY u.UserID, u.Username
                    ORDER BY AvgScore DESC",
                            new { StudentIDs = studentIds, CourseId = courseId }).ToList();
                    }

                    return stats;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при получении статистики группы: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Проверить, есть ли у группы назначенный курс
        /// </summary>
        public static bool GroupHasCourse(int groupId)
        {
            const string sql = "SELECT COUNT(1) FROM Groups WHERE GroupID = @GroupID AND CourseID IS NOT NULL";

            using (var connection = new SqlConnection(ConnectionString))
            {
                return connection.QueryFirstOrDefault<int>(sql, new { GroupID = groupId }) > 0;
            }
        }


        // =============================================
        // МЕТОДЫ ДЛЯ ВЛОЖЕНИЙ
        // =============================================

        public static List<TopicAttachment> GetAttachmentsByTopic(int topicId)
        {
            const string sql = @"
        SELECT AttachmentID, TopicID, FileName, StoredName, 
               FileType, FileSizeKB, UploadedAt
        FROM TopicAttachments
        WHERE TopicID = @TopicID
        ORDER BY UploadedAt DESC";

            return LoadData<TopicAttachment>(sql, new { TopicID = topicId });
        }

        public static int AddAttachment(int topicId, string fileName,
                                         string storedName, string fileType,
                                         int fileSizeKB, int uploadedBy)
        {
            const string sql = @"
        INSERT INTO TopicAttachments 
            (TopicID, FileName, StoredName, FileType, FileSizeKB, UploadedBy)
        OUTPUT INSERTED.AttachmentID
        VALUES (@TopicID, @FileName, @StoredName, @FileType, @FileSizeKB, @UploadedBy)";

            using (var connection = new SqlConnection(ConnectionString))
            {
                return connection.QuerySingle<int>(sql, new
                {
                    TopicID = topicId,
                    FileName = fileName,
                    StoredName = storedName,
                    FileType = fileType,
                    FileSizeKB = fileSizeKB,
                    UploadedBy = uploadedBy
                });
            }
        }

        public static void DeleteAttachment(int attachmentId)
        {
            const string sql = "DELETE FROM TopicAttachments WHERE AttachmentID = @ID";
            ExecuteSql(sql, new { ID = attachmentId });
        }

        // ──────────────────────────────────────────
        // ССЫЛКИ (TopicLinks)
        // ──────────────────────────────────────────

        public static List<TopicLink> GetLinksByTopic(int topicId)
        {
            const string sql = @"
        SELECT LinkID, TopicID, Title, URL, Description, AddedAt
        FROM TopicLinks
        WHERE TopicID = @TopicID
        ORDER BY AddedAt DESC";

            return LoadData<TopicLink>(sql, new { TopicID = topicId });
        }

        public static void AddTopicLink(int topicId, string title,
                                         string url, string description, int addedBy)
        {
            const string sql = @"
        INSERT INTO TopicLinks (TopicID, Title, URL, Description, AddedBy)
        VALUES (@TopicID, @Title, @URL, @Description, @AddedBy)";

            ExecuteSql(sql, new
            {
                TopicID = topicId,
                Title = title,
                URL = url,
                Description = description,
                AddedBy = addedBy
            });
        }

        public static void DeleteTopicLink(int linkId)
        {
            ExecuteSql("DELETE FROM TopicLinks WHERE LinkID = @ID",
                       new { ID = linkId });
        }

        // =============================================
        // ЛАБОРАТОРНЫЕ РАБОТЫ И ОТЧЕТЫ
        // =============================================

        public static List<LabWork> GetLabWorksByCourse(int courseId, bool includeInactive = false)
        {
            const string sql = @"
                SELECT LabWorkID, CourseID, Title, Description, Deadline, IsActive, CreatedAt, CreatedBy,
                       FileName, StoredName, FilePath, FileSizeKB, FileUploadedAt
                FROM LabWorks
                WHERE CourseID = @CourseID
                  AND (@IncludeInactive = 1 OR IsActive = 1)
                ORDER BY ISNULL(Deadline, '9999-12-31'), CreatedAt DESC";

            return LoadData<LabWork>(sql, new { CourseID = courseId, IncludeInactive = includeInactive ? 1 : 0 });
        }

        public static int AddLabWork(int courseId, string title, string description, DateTime? deadline, int createdBy)
        {
            const string sql = @"
                INSERT INTO LabWorks (CourseID, Title, Description, Deadline, IsActive, CreatedBy, CreatedAt)
                OUTPUT INSERTED.LabWorkID
                VALUES (@CourseID, @Title, @Description, @Deadline, 1, @CreatedBy, GETDATE())";

            using (var connection = new SqlConnection(ConnectionString))
            {
                return connection.QuerySingle<int>(sql, new
                {
                    CourseID = courseId,
                    Title = title,
                    Description = description,
                    Deadline = deadline,
                    CreatedBy = createdBy
                });
            }
        }
        public static void UpdateLabWork(int labWorkId, string title, string description, DateTime? deadline)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                const string sql = @"
            UPDATE LabWorks
            SET Title       = @Title,
                Description = @Description,
                Deadline    = @Deadline
            WHERE LabWorkID = @LabWorkId";

                connection.Execute(sql, new
                {
                    LabWorkId = labWorkId,
                    Title = title,
                    Description = description,
                    Deadline = deadline
                });
            }
        }
        public static void DeleteLabWork(int labWorkId)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Удаляем отчёты студентов
                        connection.Execute(
                            "DELETE FROM LabReportSubmissions WHERE LabWorkID = @Id",
                            new { Id = labWorkId },
                            transaction);

                        // 2. Удаляем файл задания (если таблица называется иначе — поправьте)
                        connection.Execute(
                            "DELETE FROM LabAssignmentFiles WHERE LabWorkID = @Id",
                            new { Id = labWorkId },
                            transaction);

                        // 3. Удаляем саму лабораторную
                        connection.Execute(
                            "DELETE FROM LabWorks WHERE LabWorkID = @Id",
                            new { Id = labWorkId },
                            transaction);

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public static void SaveLabAssignmentFile(int labWorkId, string fileName, string storedName, string filePath, int fileSizeKb)
        {
            const string sql = @"
                UPDATE LabWorks
                SET FileName = @FileName,
                    StoredName = @StoredName,
                    FilePath = @FilePath,
                    FileSizeKB = @FileSizeKB,
                    FileUploadedAt = GETDATE()
                WHERE LabWorkID = @LabWorkID";

            ExecuteSql(sql, new
            {
                LabWorkID = labWorkId,
                FileName = fileName,
                StoredName = storedName,
                FilePath = filePath,
                FileSizeKB = fileSizeKb
            });
        }

        public static void SaveLabReportSubmission(int labWorkId, int userId, string fileName, string storedName, string filePath, int fileSizeKb, string comment)
        {
            const string sql = @"
                INSERT INTO LabReportSubmissions (LabWorkID, UserID, FileName, StoredName, FilePath, FileSizeKB, Comment, UploadedAt)
                VALUES (@LabWorkID, @UserID, @FileName, @StoredName, @FilePath, @FileSizeKB, @Comment, GETDATE())";

            ExecuteSql(sql, new
            {
                LabWorkID = labWorkId,
                UserID = userId,
                FileName = fileName,
                StoredName = storedName,
                FilePath = filePath,
                FileSizeKB = fileSizeKb,
                Comment = comment
            });
        }

        public static List<LabReportSubmission> GetLabReportSubmissionsByUser(int userId, int courseId)
        {
            const string sql = @"
                SELECT s.SubmissionID, s.LabWorkID, s.UserID, s.FileName, s.StoredName, s.FilePath, s.FileSizeKB, s.UploadedAt, s.Comment,
                       u.Username,
                       l.Title AS LabTitle
                FROM LabReportSubmissions s
                INNER JOIN LabWorks l ON l.LabWorkID = s.LabWorkID
                INNER JOIN Users u ON u.UserID = s.UserID
                WHERE s.UserID = @UserID AND l.CourseID = @CourseID
                ORDER BY s.UploadedAt DESC";

            return LoadData<LabReportSubmission>(sql, new { UserID = userId, CourseID = courseId });
        }

        public static List<LabReportSubmission> GetLabReportSubmissionsByLab(int labWorkId)
        {
            const string sql = @"
                SELECT s.SubmissionID, s.LabWorkID, s.UserID, s.FileName, s.StoredName, s.FilePath, s.FileSizeKB, s.UploadedAt, s.Comment,
                       u.Username,
                       l.Title AS LabTitle
                FROM LabReportSubmissions s
                INNER JOIN Users u ON u.UserID = s.UserID
                INNER JOIN LabWorks l ON l.LabWorkID = s.LabWorkID
                WHERE s.LabWorkID = @LabWorkID
                ORDER BY s.UploadedAt DESC";

            return LoadData<LabReportSubmission>(sql, new { LabWorkID = labWorkId });
        }
        // =============================================
        // РЕГИСТРАЦИЯ И ВЕРИФИКАЦИЯ БЕЗ EMAIL
        // =============================================

        /// <summary>
        /// Регистрирует пользователя с IsEmailVerified = 1 (без проверки email).
        /// Аккаунт сразу активен.
        /// </summary>
        public static bool RegisterUserVerified(string username, string email, string password)
        {
            try
            {
                string hashedPassword = HashPassword(password);
                const string query = @"
                    INSERT INTO dbo.Users (Username, Email, PasswordHash, RoleID, RegistrationDate, IsEmailVerified)
                    VALUES (@Username, @Email, @PasswordHash, 3, GETDATE(), 1)";

                ExecuteSql(query, new
                {
                    Username = username,
                    Email = email,
                    PasswordHash = hashedPassword
                });
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Ошибка при регистрации: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Вручную подтверждает аккаунт пользователя (вызывается администратором).
        /// </summary>
        public static void ManuallyVerifyUser(int userId)
        {
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        conn.Execute(
                            "UPDATE EmailVerifications SET IsUsed = 1 WHERE UserID = @UserID AND IsUsed = 0",
                            new { UserID = userId }, tx);

                        conn.Execute(
                            "UPDATE Users SET IsEmailVerified = 1 WHERE UserID = @UserID",
                            new { UserID = userId }, tx);

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Возвращает актуальный код верификации пользователя (если он ещё не истёк).
        /// Используется в AdminPanel для показа кода администратору.
        /// </summary>
        public static string GetPendingVerificationCode(int userId)
        {
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    return conn.QueryFirstOrDefault<string>(@"
                        SELECT TOP 1 Code
                        FROM EmailVerifications
                        WHERE UserID = @UserID
                          AND IsUsed = 0
                          AND ExpiresAt > GETDATE()
                        ORDER BY CreatedAt DESC",
                        new { UserID = userId });
                }
            }
            catch { return null; }
        }

        // =============================================
        // ПРИВЯЗКА ПРЕПОДАВАТЕЛЕЙ К КУРСАМ
        // =============================================

        /// <summary>
        /// Создаёт таблицу TeacherCourses если её нет. Вызывается при запуске.
        /// </summary>
        public static void EnsureTeacherCoursesSchema()
        {
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Execute(@"
                        IF NOT EXISTS (SELECT 1 FROM sys.objects
                                       WHERE object_id = OBJECT_ID(N'TeacherCourses') AND type='U')
                        CREATE TABLE TeacherCourses (
                            ID          INT IDENTITY(1,1) PRIMARY KEY,
                            TeacherID   INT NOT NULL,
                            CourseID    INT NOT NULL,
                            AssignedAt  DATETIME NOT NULL DEFAULT GETDATE(),
                            AssignedBy  INT NULL,
                            CONSTRAINT UQ_TeacherCourse UNIQUE (TeacherID, CourseID)
                        );

                        IF NOT EXISTS (SELECT 1 FROM sys.columns
                                       WHERE object_id = OBJECT_ID('LabWorks') AND name = 'TeacherID')
                            ALTER TABLE LabWorks ADD TeacherID INT NULL;
                    ");

                    // Администраторы автоматически получают доступ ко всем курсам
                    conn.Execute(@"
                        INSERT INTO TeacherCourses (TeacherID, CourseID, AssignedBy)
                        SELECT u.UserID, c.CourseID, NULL
                        FROM Users u CROSS JOIN Courses c
                        WHERE u.RoleID = 1 AND c.IsActive = 1
                          AND NOT EXISTS (
                              SELECT 1 FROM TeacherCourses tc
                              WHERE tc.TeacherID = u.UserID AND tc.CourseID = c.CourseID
                          );
                    ");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EnsureTeacherCoursesSchema] {ex.Message}");
            }
        }

        /// <summary>
        /// Возвращает курсы, которые ведёт преподаватель.
        /// Администратор получает все активные курсы.
        /// </summary>
        public static List<Course> GetCoursesForTeacher(int userId)
        {
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    int roleId = conn.QueryFirstOrDefault<int>(
                        "SELECT RoleID FROM Users WHERE UserID = @UserID",
                        new { UserID = userId });

                    if (roleId == 1) return GetAllCourses(); // Админ видит всё

                    const string sql = @"
                        SELECT c.CourseID, c.CourseName, c.CourseDescription, c.IsActive, c.CreatedAt, c.UpdatedAt
                        FROM TeacherCourses tc
                        JOIN Courses c ON c.CourseID = tc.CourseID AND c.IsActive = 1
                        WHERE tc.TeacherID = @TeacherID
                        ORDER BY c.CourseName";

                    return conn.Query<Course>(sql, new { TeacherID = userId }).ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetCoursesForTeacher] {ex.Message}");
                return GetAllCourses(); // fallback — вернуть все курсы при ошибке
            }
        }

        /// <summary>
        /// Проверяет, может ли пользователь управлять лабораторными в данном курсе.
        /// Администратор — всегда может. Преподаватель — только в своих курсах.
        /// </summary>
        public static bool CanManageLabsInCourse(int userId, int courseId)
        {
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    int roleId = conn.QueryFirstOrDefault<int>(
                        "SELECT RoleID FROM Users WHERE UserID = @UserID",
                        new { UserID = userId });

                    if (roleId == 1) return true;  // Администратор
                    if (roleId != 2) return false; // Не преподаватель

                    // Проверяем таблицу TeacherCourses
                    bool tableExists = conn.QueryFirstOrDefault<int>(@"
                        SELECT COUNT(1) FROM sys.objects
                        WHERE object_id = OBJECT_ID(N'TeacherCourses') AND type='U'") > 0;

                    if (!tableExists) return true; // Таблица ещё не создана — разрешаем

                    return conn.QueryFirstOrDefault<int>(
                        "SELECT COUNT(1) FROM TeacherCourses WHERE TeacherID = @UserID AND CourseID = @CourseID",
                        new { UserID = userId, CourseID = courseId }) > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CanManageLabsInCourse] {ex.Message}");
                return true; // При ошибке — не блокируем
            }
        }

        /// <summary>
        /// Назначает преподавателя на курс. Вызывается администратором.
        /// </summary>
        public static void AssignTeacherToCourse(int teacherId, int courseId, int assignedBy)
        {
            const string sql = @"
                IF NOT EXISTS (SELECT 1 FROM TeacherCourses WHERE TeacherID = @TeacherID AND CourseID = @CourseID)
                    INSERT INTO TeacherCourses (TeacherID, CourseID, AssignedBy)
                    VALUES (@TeacherID, @CourseID, @AssignedBy)";
            ExecuteSql(sql, new { TeacherID = teacherId, CourseID = courseId, AssignedBy = assignedBy });
        }

        /// <summary>
        /// Снимает преподавателя с курса. Вызывается администратором.
        /// </summary>
        public static void UnassignTeacherFromCourse(int teacherId, int courseId)
        {
            const string sql = "DELETE FROM TeacherCourses WHERE TeacherID = @TeacherID AND CourseID = @CourseID";
            ExecuteSql(sql, new { TeacherID = teacherId, CourseID = courseId });
        }

        /// <summary>
        /// Возвращает всех преподавателей для курса с флагом IsAssigned.
        /// </summary>
        public static List<TeacherCourseAssignment> GetTeacherAssignmentsForCourse(int courseId)
        {
            const string sql = @"
                SELECT
                    u.UserID            AS TeacherID,
                    u.Username          AS TeacherName,
                    u.Email,
                    c.CourseID          AS CourseID,
                    c.CourseName        AS CourseName,
                    CASE WHEN tc.ID IS NOT NULL THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsAssigned,
                    tc.AssignedAt
                FROM Users u
                CROSS JOIN Courses c
                LEFT JOIN TeacherCourses tc ON tc.TeacherID = u.UserID AND tc.CourseID = c.CourseID
                WHERE u.RoleID = 2 AND c.CourseID = @CourseID AND c.IsActive = 1
                ORDER BY u.Username";
            return LoadData<TeacherCourseAssignment>(sql, new { CourseID = courseId });
        }

        /// <summary>
        /// Возвращает ВСЕ существующие назначения преподаватель+курс.
        /// Для таблицы в AdminPanel.
        /// </summary>
        public static List<TeacherCourseAssignment> GetAllTeacherAssignments()
        {
            try
            {
                const string sql = @"
                    SELECT
                        u.UserID        AS TeacherID,
                        u.Username      AS TeacherName,
                        u.Email,
                        c.CourseID      AS CourseID,
                        c.CourseName    AS CourseName,
                        CAST(1 AS BIT)  AS IsAssigned,
                        tc.AssignedAt
                    FROM TeacherCourses tc
                    JOIN Users u ON u.UserID = tc.TeacherID AND u.RoleID = 2
                    JOIN Courses c ON c.CourseID = tc.CourseID AND c.IsActive = 1
                    ORDER BY c.CourseName, u.Username";
                return LoadData<TeacherCourseAssignment>(sql, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetAllTeacherAssignments] {ex.Message}");
                return new List<TeacherCourseAssignment>();
            }
        }


    }

}