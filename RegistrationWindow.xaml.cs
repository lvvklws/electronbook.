using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Computer_networks.Models;
using Computer_networks.Data;
using Computer_networks.Utilities;
using Computer_networks.Services;
using Computer_networks.Views;
using MaterialDesignThemes.Wpf;

namespace Computer_networks
{
    public partial class RegistrationWindow : Window
    {
        private readonly Regex _emailRegex = new Regex(
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
            RegexOptions.Compiled);

        public RegistrationWindow()
        {
            InitializeComponent();
            LoginTextBox.Focus();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void ShowPasswordCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            VisiblePasswordTextBox.Text = PasswordBox.Password;
            PasswordBox.Visibility = Visibility.Collapsed;
            VisiblePasswordTextBox.Visibility = Visibility.Visible;
            VisiblePasswordTextBox.Focus();
        }

        private void ShowPasswordCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            PasswordBox.Visibility = Visibility.Visible;
            VisiblePasswordTextBox.Visibility = Visibility.Collapsed;
            PasswordBox.Focus();
        }

        // ─── ВХОД ────────────────────────────────────────────────────────
        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string login = LoginTextBox.Text.Trim();
                string password = GetPassword();

                if (!ValidateLoginInput(login, password)) return;

                User user = SqlDataAccess.AuthenticateUser(login, password);

                if (user != null)
                {
                    UserManager.Login(user);
                    ShowSuccessMessage($"Добро пожаловать, {user.Username}!");
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    ShowErrorMessage("Неверный логин или пароль.", "Ошибка аутентификации");
                }
            }
            catch (Exception ex)
            {
                HandleException(ex, "Ошибка при входе в систему");
            }
        }

        // ─── РЕГИСТРАЦИЯ ─────────────────────────────────────────────────
        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string email = LoginTextBox.Text.Trim();
                string username = email.Contains("@") ? email.Split('@')[0] : email;
                string password = GetPassword();

                if (!ValidateRegistrationInput(username, email, password)) return;

                if (SqlDataAccess.IsEmailExists(email))
                {
                    ShowErrorMessage(
                        "Пользователь с таким email уже существует. Используйте другой email.",
                        "Ошибка регистрации");
                    LoginTextBox.Focus();
                    LoginTextBox.SelectAll();
                    return;
                }

                if (SqlDataAccess.IsUsernameExists(username))
                {
                    string suffix = new Random().Next(100, 999).ToString();
                    username = username + suffix;
                    MessageBox.Show(
                        $"Имя пользователя уже занято. Вам автоматически назначено имя: «{username}».\n" +
                        "Вы сможете изменить его в профиле после входа.",
                        "Имя скорректировано",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // ═══ РЕГИСТРАЦИЯ БЕЗ ОБЯЗАТЕЛЬНОЙ ВЕРИФИКАЦИИ ═══
                // Аккаунт создаётся сразу активным (IsEmailVerified = 1).
                // Администратор может управлять пользователями через панель.
                bool result = SqlDataAccess.RegisterUserVerified(username, email, password);

                if (result)
                {
                    ShowSuccessMessage(
                        $"Регистрация прошла успешно!\n\n" +
                        $"Ваш логин: {username}\n\n" +
                        "Теперь вы можете войти в систему.");
                    ClearPasswordField();
                }
                else
                {
                    ShowErrorMessage(
                        "Произошла ошибка при регистрации. Попробуйте позже.",
                        "Ошибка регистрации");
                }
            }
            catch (Exception ex)
            {
                HandleException(ex, "Ошибка при регистрации");
            }
        }

        // ─── ВАЛИДАЦИЯ ───────────────────────────────────────────────────
        private bool ValidateLoginInput(string login, string password)
        {
            if (string.IsNullOrWhiteSpace(login))
            {
                ShowWarningMessage("Пожалуйста, введите логин.", "Не заполнено обязательное поле");
                LoginTextBox.Focus();
                return false;
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                ShowWarningMessage("Пожалуйста, введите пароль.", "Не заполнено обязательное поле");
                FocusPasswordBox();
                return false;
            }
            if (password.Length < 6)
            {
                ShowWarningMessage("Пароль должен содержать минимум 6 символов.", "Некорректный пароль");
                FocusPasswordBox();
                return false;
            }
            return true;
        }

        private bool ValidateRegistrationInput(string username, string email, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
            {
                ShowWarningMessage("Имя пользователя должно содержать минимум 3 символа.", "Некорректное имя");
                LoginTextBox.Focus();
                return false;
            }
            if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
            {
                ShowWarningMessage("Введите корректный email адрес.", "Ошибка в формате email");
                LoginTextBox.Focus();
                return false;
            }
            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                ShowWarningMessage("Пароль должен содержать минимум 6 символов.", "Слишком короткий пароль");
                FocusPasswordBox();
                return false;
            }
            if (password.Equals(username, StringComparison.OrdinalIgnoreCase))
            {
                ShowWarningMessage("Пароль не должен совпадать с логином.", "Ненадёжный пароль");
                FocusPasswordBox();
                return false;
            }
            return true;
        }

        private bool IsValidEmail(string email) => _emailRegex.IsMatch(email);

        // ─── ВСПОМОГАТЕЛЬНЫЕ ─────────────────────────────────────────────
        private string GetPassword() =>
            PasswordBox.Visibility == Visibility.Visible
                ? PasswordBox.Password
                : VisiblePasswordTextBox.Text;

        private void FocusPasswordBox()
        {
            if (PasswordBox.Visibility == Visibility.Visible) PasswordBox.Focus();
            else VisiblePasswordTextBox.Focus();
        }

        private void ShowSuccessMessage(string message) =>
            MessageBox.Show(message, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

        private void ShowErrorMessage(string message, string caption = "Ошибка") =>
            MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);

        private void ShowWarningMessage(string message, string caption = "Предупреждение") =>
            MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Warning);

        private void HandleException(Exception ex, string context) =>
            ShowErrorMessage($"{context}: {ex.Message}", "Системная ошибка");

        private void ClearPasswordField()
        {
            PasswordBox.Password = "";
            VisiblePasswordTextBox.Text = "";
        }

        private void ShowLoginError(string message)
        {
            if (LoginErrorText == null) return;
            LoginErrorText.Text = message;
            LoginErrorText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ShowPasswordError(string message)
        {
            if (PasswordErrorText == null) return;
            PasswordErrorText.Text = message;
            PasswordErrorText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void LoginTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string text = LoginTextBox?.Text ?? "";
            if (string.IsNullOrWhiteSpace(text)) ShowLoginError("Введите логин или email");
            else if (text.Length < 3) ShowLoginError("Минимум 3 символа");
            else ShowLoginError("");
        }

        private void LoginTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) FocusPasswordBox();
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) LoginButton_Click(sender, e);
        }
    }
}