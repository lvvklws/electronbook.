using System.Windows;
using System.Windows.Input;

namespace Computer_networks.Views
{
    public partial class CourseEditDialog : Window
    {
        public string CourseName => NameBox.Text.Trim();
        public string CourseDescription => DescriptionBox.Text.Trim();

        // Новый курс
        public CourseEditDialog()
        {
            InitializeComponent();
            TitleText.Text = "📚 Новый курс";
            NameBox.Focus();
        }

        // Редактирование существующего
        public CourseEditDialog(string name, string description)
        {
            InitializeComponent();
            TitleText.Text = "✏️ Редактировать курс";
            NameBox.Text = name;
            DescriptionBox.Text = description;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                ErrorText.Text = "Введите название курса.";
                ErrorText.Visibility = Visibility.Visible;
                NameBox.Focus();
                return;
            }
            if (NameBox.Text.Trim().Length < 3)
            {
                ErrorText.Text = "Название должно содержать минимум 3 символа.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}