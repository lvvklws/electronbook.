using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Computer_networks.Data;
using Computer_networks.Models;
using Computer_networks.Utilities;

namespace Computer_networks.Views
{
    public partial class ProfileWindow : Window
    {
        private readonly User _user;

        // Выбранный аватар (эмодзи)
        private string _selectedAvatar = "👤";

        /// <summary>Аватар после закрытия окна — читает MainWindow для обновления тулбара</summary>
        public string SelectedAvatar => _selectedAvatar;

        public ProfileWindow()
        {
            InitializeComponent();
            _user = UserManager.CurrentUser;

            if (_user == null)
            {
                MessageBox.Show("Ошибка: пользователь не авторизован.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            LoadUserData();
            ConfigureForRole();
        }

        // ── Загрузка данных ─────────────────────────────────────────
        private void LoadUserData()
        {
            // Шапка
            HeaderUsername.Text = _user.Username;
            HeaderRole.Text = _user.RoleName;
            HeaderEmail.Text = _user.Email;

            // Поля профиля
            UsernameBox.Text = _user.Username;
            EmailBox.Text = _user.Email;

            // Аватар: сначала пробуем из БД, затем — дефолт по роли
            string savedAvatar = SqlDataAccess.GetUserAvatar(_user.UserID);
            _selectedAvatar = string.IsNullOrEmpty(savedAvatar)
                ? GetDefaultAvatarForRole(_user.RoleID)
                : savedAvatar;

            SetAvatarRadioButton(_selectedAvatar);
            AvatarEmoji.Text = _selectedAvatar;
        }

        private string GetDefaultAvatarForRole(int roleId)
        {
            if (roleId == 1) return "👑";
            if (roleId == 2) return "📚";
            return "🎓";
        }

        private void SetAvatarRadioButton(string emoji)
        {
            foreach (var rb in new[] { AvatarAdmin, AvatarTeacher, AvatarStudent,
                                        AvatarCode, AvatarStar, AvatarRobot })
            {
                if (rb.Tag?.ToString() == emoji)
                {
                    rb.IsChecked = true;
                    break;
                }
            }
        }

        // ── Настройка UI под роль ────────────────────────────────────
        private void ConfigureForRole()
        {
            if (_user.IsTeacher)
            {
                GroupsTabItem.Visibility = Visibility.Visible;
                GroupsHeaderText.Text = "Группы, которыми вы управляете:";
                GroupsFooterText.Text = "Выберите группу и нажмите «Статистика группы»";
                ViewGroupStatsButton.Visibility = Visibility.Visible;
                LoadTeacherGroups();
            }
            else if (_user.IsStudent)
            {
                GroupsTabItem.Visibility = Visibility.Visible;
                GroupsHeaderText.Text = "Группы, в которых вы состоите:";
                GroupsFooterText.Text = "Для записи в группу обратитесь к преподавателю";
                ViewGroupStatsButton.Visibility = Visibility.Collapsed;
                LoadStudentGroups();
            }
            else
            {
                GroupsTabItem.Visibility = Visibility.Collapsed;
            }
        }

        // ── Группы преподавателя ─────────────────────────────────────
        private void LoadTeacherGroups()
        {
            try
            {
                var allGroups = SqlDataAccess.GetAllGroups();

                if (allGroups == null || allGroups.Count == 0)
                {
                    GroupsListView.Visibility = Visibility.Collapsed;
                    NoGroupsText.Visibility = Visibility.Visible;
                }
                else
                {
                    GroupsListView.ItemsSource = allGroups;
                    GroupsListView.Visibility = Visibility.Visible;
                    NoGroupsText.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                // Показываем ошибку в UI вместо краша
                GroupsListView.Visibility = Visibility.Collapsed;
                NoGroupsText.Text = $"⚠ Не удалось загрузить группы:\n{ex.Message}";
                NoGroupsText.Visibility = Visibility.Visible;
                System.Diagnostics.Debug.WriteLine($"[LoadTeacherGroups] SqlException: {ex}");
            }
        }

        // ── Группы студента ──────────────────────────────────────────
        private void LoadStudentGroups()
        {
            try
            {
                var myGroups = SqlDataAccess.GetStudentGroups(_user.UserID);

                if (myGroups == null || myGroups.Count == 0)
                {
                    GroupsListView.Visibility = Visibility.Collapsed;
                    NoGroupsText.Text = "📭 Вы пока не добавлены ни в одну группу.";
                    NoGroupsText.Visibility = Visibility.Visible;
                }
                else
                {
                    GroupsListView.ItemsSource = myGroups;
                    GroupsListView.Visibility = Visibility.Visible;
                    NoGroupsText.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Ошибка загрузки групп: {ex.Message}", isError: true);
            }
        }

        // ── Выбор группы в списке ────────────────────────────────────
        private void GroupsListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ViewGroupStatsButton != null)
                ViewGroupStatsButton.IsEnabled = GroupsListView.SelectedItem != null;
        }

        // ── Кнопка статистики группы (только преподаватель) ──────────
        private void ViewGroupStatsButton_Click(object sender, RoutedEventArgs e)
        {
            if (GroupsListView.SelectedItem is Computer_networks.Models.Group group)
            {
                int courseId = group.CourseID ?? Computer_networks.Data.SqlDataAccess.CurrentCourseId;
                var statsWindow = new Computer_networks.Views.GroupStatsWindow(group.GroupID, courseId);
                statsWindow.Owner = this;
                statsWindow.ShowDialog();
            }
        }

        // ── Аватар ──────────────────────────────────────────────────
        private void Avatar_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton rb && rb.Tag != null)
            {
                _selectedAvatar = rb.Tag.ToString();
                AvatarEmoji.Text = _selectedAvatar;
                HeaderUsername.Text = _user.Username; // обновляем на случай изменения
            }
        }

        // ── Сохранение профиля ───────────────────────────────────────
        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            string newUsername = UsernameBox.Text.Trim();
            string newEmail = EmailBox.Text.Trim();

            // Валидация
            if (string.IsNullOrWhiteSpace(newUsername))
            {
                ShowStatus("Имя пользователя не может быть пустым.", isError: true);
                return;
            }
            if (newUsername.Length < 3)
            {
                ShowStatus("Имя пользователя должно содержать минимум 3 символа.", isError: true);
                return;
            }
            if (string.IsNullOrWhiteSpace(newEmail) || !newEmail.Contains("@"))
            {
                ShowStatus("Введите корректный email.", isError: true);
                return;
            }

            try
            {
                // Проверяем, что email не занят другим пользователем
                if (newEmail != _user.Email && SqlDataAccess.IsEmailExists(newEmail))
                {
                    ShowStatus("Этот email уже используется другим пользователем.", isError: true);
                    return;
                }

                // Обновляем объект и БД
                _user.Username = newUsername;
                _user.Email = newEmail;
                SqlDataAccess.UpdateUser(_user);

                // Сохраняем аватар в UserSettings
                SqlDataAccess.SaveUserAvatar(_user.UserID, _selectedAvatar);

                // Обновляем UserManager (текущий сеанс)
                UserManager.Login(_user);

                // Обновляем шапку
                HeaderUsername.Text = newUsername;
                HeaderEmail.Text = newEmail;
                AvatarEmoji.Text = _selectedAvatar;

                ShowStatus("✅ Профиль успешно сохранён!", isError: false);
            }
            catch (Exception ex)
            {
                ShowStatus($"Ошибка сохранения: {ex.Message}", isError: true);
            }
        }

        // ── Смена пароля ─────────────────────────────────────────────
        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            string oldPwd = OldPasswordBox.Password;
            string newPwd = NewPasswordBox.Password;
            string confirm = ConfirmPasswordBox.Password;

            if (string.IsNullOrEmpty(oldPwd))
            {
                ShowStatus("Введите текущий пароль.", isError: true);
                return;
            }

            // Проверяем старый пароль
            string oldHash = HashPassword(oldPwd);
            if (oldHash != _user.PasswordHash)
            {
                ShowStatus("Текущий пароль введён неверно.", isError: true);
                return;
            }

            if (string.IsNullOrEmpty(newPwd) || newPwd.Length < 6)
            {
                ShowStatus("Новый пароль должен содержать минимум 6 символов.", isError: true);
                return;
            }

            if (newPwd != confirm)
            {
                ShowStatus("Новый пароль и подтверждение не совпадают.", isError: true);
                return;
            }

            if (newPwd == oldPwd)
            {
                ShowStatus("Новый пароль должен отличаться от текущего.", isError: true);
                return;
            }

            try
            {
                string newHash = HashPassword(newPwd);

                SqlDataAccess.ExecuteSql(
                    "UPDATE dbo.Users SET PasswordHash = @Hash WHERE UserID = @UserID",
                    new { Hash = newHash, UserID = _user.UserID });

                // Обновляем объект в памяти
                _user.PasswordHash = newHash;
                UserManager.Login(_user);

                // Очищаем поля
                OldPasswordBox.Clear();
                NewPasswordBox.Clear();
                ConfirmPasswordBox.Clear();

                ShowStatus("✅ Пароль успешно изменён!", isError: false);
            }
            catch (Exception ex)
            {
                ShowStatus($"Ошибка смены пароля: {ex.Message}", isError: true);
            }
        }

        // ── Хэширование (SHA-256, как в SqlDataAccess) ───────────────
        private static string HashPassword(string password)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                var sb = new StringBuilder();
                foreach (byte b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // ── Статус-баннер ────────────────────────────────────────────
        private void ShowStatus(string message, bool isError)
        {
            StatusText.Text = message;
            StatusBanner.Background = new SolidColorBrush(
                isError
                    ? Color.FromRgb(255, 235, 235)
                    : Color.FromRgb(230, 255, 235));
            StatusText.Foreground = new SolidColorBrush(
                isError
                    ? Color.FromRgb(180, 0, 0)
                    : Color.FromRgb(0, 130, 50));
            StatusBanner.Visibility = Visibility.Visible;

            // Скрываем через 4 секунды
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(4)
            };
            timer.Tick += (s, _) =>
            {
                timer.Stop();
                StatusBanner.Visibility = Visibility.Collapsed;
            };
            timer.Start();
        }

        // ── Перетаскивание окна ──────────────────────────────────────
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // оставляем для совместимости (MouseDown на окне)
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}