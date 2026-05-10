using System;
using System.Windows;
using System.Windows.Input;

namespace Computer_networks.Views
{
    public partial class AddLinkDialog : Window
    {
        public string LinkTitle { get; private set; }
        public string LinkUrl { get; private set; }
        public string LinkDesc { get; private set; }

        public AddLinkDialog()
        {
            InitializeComponent();
            // Фокус на первое поле при открытии
            Loaded += (s, e) => TitleBox.Focus();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Проверка названия
            if (string.IsNullOrWhiteSpace(TitleBox.Text))
            {
                MessageBox.Show("Введите название ссылки.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TitleBox.Focus();
                return;
            }

            // Проверка URL
            string url = UrlBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Введите URL.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                UrlBox.Focus();
                return;
            }

            // Добавляем https:// если пользователь забыл
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                url = "https://" + url;

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                MessageBox.Show("Введите корректный URL.\nПример: https://docs.microsoft.com",
                    "Некорректный URL", MessageBoxButton.OK, MessageBoxImage.Warning);
                UrlBox.Focus();
                return;
            }

            LinkTitle = TitleBox.Text.Trim();
            LinkUrl = url;
            LinkDesc = DescBox.Text.Trim();

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}