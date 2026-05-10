using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Computer_networks.Data;
using Computer_networks.Services;

namespace Computer_networks.Views
{
    public partial class VerificationWindow : Window
    {
        private readonly string _email;
        private readonly int _userId;
        private string _currentCode;

        private DispatcherTimer _timer;
        private int _secondsLeft = 900; // 15 минут

        public VerificationWindow(int userId, string email)
        {
            InitializeComponent();
            _userId = userId;
            _email = email;

            EmailLabel.Text = email;

            // Генерируем и отправляем первый код
            SendCode();
            StartTimer();

            D1.Focus();
        }

        // ─── Отправка кода ───────────────────────────────────────────────

        private void SendCode()
        {
            _currentCode = EmailService.GenerateCode();

            // Сохраняем в БД
            SqlDataAccess.SaveVerificationCode(_userId, _email, _currentCode, DateTime.Now.AddMinutes(15));

            // Пробуем отправить письмо в фоне
            System.Threading.Tasks.Task.Run(() =>
            {
                string error = EmailService.SendVerificationCode(_email, _currentCode);
                Dispatcher.Invoke(() =>
                {
                    if (error != null)
                    {
                        // SMTP не работает — показываем код прямо в окне
                        FallbackCodePanel.Visibility = Visibility.Visible;
                        // Добавляем пробел между цифрами для читабельности (вместо LetterSpacing)
                        FallbackCodeText.Text = string.Join(" ", _currentCode.ToCharArray());
                        InstructionText.Text = "Письмо не отправлено. Используй код ниже:";
                        ErrorText.Text = $"Почта недоступна: {error}";
                        ErrorText.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        FallbackCodePanel.Visibility = Visibility.Collapsed;
                        InstructionText.Text = $"Код отправлен на {_email}. Введи его ниже:";
                    }
                });
            });
        }

        // ─── Таймер ──────────────────────────────────────────────────────

        private void StartTimer()
        {
            _secondsLeft = 900;
            UpdateTimerText();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) =>
            {
                _secondsLeft--;
                UpdateTimerText();
                if (_secondsLeft <= 0)
                {
                    _timer.Stop();
                    TimerText.Text = "Код истёк. Отправь новый.";
                }
            };
            _timer.Start();
        }

        private void UpdateTimerText()
        {
            int m = _secondsLeft / 60;
            int s = _secondsLeft % 60;
            TimerText.Text = $"Код действителен ещё {m}:{s:D2}";
        }

        // ─── Ввод цифр ───────────────────────────────────────────────────

        private void Digit_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        private void Digit_TextChanged(object sender, TextChangedEventArgs e)
        {
            var box = (TextBox)sender;
            if (box.Text.Length == 1)
            {
                // Перемещаем фокус на следующее поле
                string nextName = box.Tag?.ToString();
                if (!string.IsNullOrEmpty(nextName))
                {
                    var next = FindName(nextName) as TextBox;
                    next?.Focus();
                    next?.SelectAll();
                }
                else
                {
                    // Последнее поле — сразу проверяем
                    TryConfirm();
                }
            }
        }

        // ─── Подтверждение ───────────────────────────────────────────────

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            TryConfirm();
        }

        private void TryConfirm()
        {
            string entered = $"{D1.Text}{D2.Text}{D3.Text}{D4.Text}{D5.Text}{D6.Text}";
            if (entered.Length < 6) return;

            ErrorText.Visibility = Visibility.Collapsed;

            bool valid = SqlDataAccess.VerifyEmailCode(_userId, _email, entered);
            if (valid)
            {
                _timer?.Stop();
                DialogResult = true;
                Close();
            }
            else
            {
                ErrorText.Text = "Неверный или истёкший код. Попробуй ещё раз.";
                ErrorText.Visibility = Visibility.Visible;
                // Очищаем поля
                D1.Text = D2.Text = D3.Text = D4.Text = D5.Text = D6.Text = "";
                D1.Focus();
            }
        }

        // ─── Повторная отправка ───────────────────────────────────────────

        private void ResendLink_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            FallbackCodePanel.Visibility = Visibility.Collapsed;
            D1.Text = D2.Text = D3.Text = D4.Text = D5.Text = D6.Text = "";
            SendCode();
            _timer?.Stop();
            StartTimer();
            D1.Focus();
            InstructionText.Text = "Отправляем новый код...";
        }

        // ─── Окно ────────────────────────────────────────────────────────

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
            DialogResult = false;
            Close();
        }
    }
}