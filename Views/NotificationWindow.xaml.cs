using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Computer_networks.Views
{
    public partial class NotificationWindow : Window
    {
        private DispatcherTimer _closeTimer;

        public NotificationWindow(string iconPath, string title, string description, int xp)
        {
            InitializeComponent();

            try
            {
                // Получаем путь к корню проекта (там где папка Images)
                string baseDir = AppDomain.CurrentDomain.BaseDirectory; // ...\bin\Debug\
                string projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..\\..\\")); // Поднимаемся на 2 уровня выше

                // Убираем первый слеш из пути
                string cleanPath = iconPath.TrimStart('/', '\\');

                // Полный путь к картинке
                string fullPath = Path.Combine(projectRoot, cleanPath);

                System.Diagnostics.Debug.WriteLine($"Ищем картинку: {fullPath}");
                System.Diagnostics.Debug.WriteLine($"Файл существует: {File.Exists(fullPath)}");

                if (File.Exists(fullPath))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    IconImage.Source = bitmap;
                    IconImage.Visibility = Visibility.Visible;
                }
                else
                {
                    IconImage.Visibility = Visibility.Collapsed;
                    System.Diagnostics.Debug.WriteLine($"Картинка не найдена!");
                }
            }
            catch (Exception ex)
            {
                IconImage.Visibility = Visibility.Collapsed;
                System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}");
            }

            TitleText.Text = title;
            DescriptionText.Text = description;
            XPText.Text = $"+{xp} XP";

            InitializeWindow();
        }

        private void InitializeWindow()
        {
            var desktop = SystemParameters.WorkArea;
            Left = desktop.Right - Width - 20;
            Top = desktop.Top + 20;

            var showStoryboard = (Storyboard)Resources["ShowAnimation"];
            showStoryboard.Begin(this);

            _closeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _closeTimer.Tick += (s, e) =>
            {
                _closeTimer.Stop();
                StartCloseAnimation();
            };
            _closeTimer.Start();

            this.MouseLeftButtonUp += (s, e) =>
            {
                _closeTimer?.Stop();
                StartCloseAnimation();
            };
        }

        private void StartCloseAnimation()
        {
            var hideStoryboard = (Storyboard)Resources["HideAnimation"];
            hideStoryboard.Completed += (s, e) => Close();
            hideStoryboard.Begin(this);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _closeTimer?.Stop();
            StartCloseAnimation();
        }
    }
}