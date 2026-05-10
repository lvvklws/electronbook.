using Computer_networks.Models;

namespace Computer_networks.Utilities
{
    public static class UserManager
    {
        // Храним текущего пользователя
        private static User _currentUser;

        public static User CurrentUser
        {
            get => _currentUser;
            private set => _currentUser = value;
        }

        public static bool IsLoggedIn => _currentUser != null;

        /// <summary>
        /// Вход пользователя в систему
        /// </summary>
        public static void Login(User user)
        {
            if (user == null)
                throw new System.ArgumentNullException(nameof(user));

            // ВАЖНО: Создаём НОВЫЙ объект, а не сохраняем ссылку
            // Это предотвращает кэширование старых данных
            _currentUser = new User
            {
                UserID = user.UserID,
                Username = user.Username,
                Email = user.Email,
                PasswordHash = user.PasswordHash,
                RoleID = user.RoleID,  // ← КРИТИЧЕСКИ ВАЖНО
                RegistrationDate = user.RegistrationDate
            };

            // Отладочный лог
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[UserManager] Login: {_currentUser.Username}, RoleID={_currentUser.RoleID}");
#endif
        }

        /// <summary>
        /// Выход из системы — ПОЛНАЯ очистка
        /// </summary>
        public static void Logout()
        {
#if DEBUG
            if (_currentUser != null)
                System.Diagnostics.Debug.WriteLine($"[UserManager] Logout: {_currentUser.Username}");
#endif
            _currentUser = null;
        }

        public static int GetCurrentUserId() => _currentUser?.UserID ?? 0;

        // ─── Методы проверки прав ──────────────────────────────────
        public static bool CanAccessAdminPanel()
        {
            return _currentUser?.IsAdmin == true || _currentUser?.IsTeacher == true;
        }

        public static bool CanManageContent()
        {
            return _currentUser?.CanManageContent == true;
        }

        public static bool CanManageUsers()
        {
            return _currentUser?.CanManageUsers == true;
        }

        public static bool CanViewStudentStatistics()
        {
            return _currentUser?.IsAdmin == true || _currentUser?.IsTeacher == true;
        }

        // ─── Отладочная информация ─────────────────────────────────
        /// <summary>
        /// Для отладки — показывает текущее состояние
        /// </summary>
        public static string GetDebugInfo()
        {
            if (_currentUser == null)
                return "НЕ АВТОРИЗОВАН";

            return $"User: {_currentUser.Username}, RoleID: {_currentUser.RoleID}, " +
                   $"IsAdmin: {_currentUser.IsAdmin}, IsTeacher: {_currentUser.IsTeacher}";
        }
    }
}