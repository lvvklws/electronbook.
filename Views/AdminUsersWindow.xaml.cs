using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Computer_networks.Data;
using Computer_networks.Models;
using Computer_networks.ViewModels;

namespace Computer_networks.Views
{
    public partial class AdminUsersWindow : Window
    {
        private int _currentCourseId = 2;
        private AdminUsersViewModel _viewModel;

        // ID текущего авторизованного администратора (передаётся снаружи)
        private int _currentAdminId;

        public AdminUsersWindow(int currentAdminId = 0)
        {
            InitializeComponent();
            _currentAdminId = currentAdminId;
            _viewModel = new AdminUsersViewModel();
            DataContext = _viewModel;
            LoadCourses();
            KeyDown += Window_KeyDown;
        }

        private void LoadCourses()
        {
            try
            {
                var courses = SqlDataAccess.GetAllCourses();
                CourseSelector.ItemsSource = courses;
                CourseSelector.SelectedValue = _currentCourseId;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки курсов: {ex.Message}");
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void CourseSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CourseSelector.SelectedValue is int courseId)
            {
                _currentCourseId = courseId;
                UpdateStatistics();
            }
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateStatistics();
        }

        // ══════════════════════════════════════════════════════════════════
        //  ЗАЩИТЫ ПРИ РЕДАКТИРОВАНИИ ЯЧЕЕК
        // ══════════════════════════════════════════════════════════════════

        private void UsersDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
                return;

            var user = e.Row.Item as User;
            if (user == null) return;

            string columnHeader = (e.Column.Header as string) ?? string.Empty;

            // 1. Нельзя редактировать собственную учётную запись через таблицу
            if (user.UserID == _currentAdminId)
            {
                MessageBox.Show(
                    "Нельзя редактировать собственную учётную запись через эту таблицу.\n" +
                    "Используйте раздел профиля.",
                    "Запрет редактирования",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                e.Cancel = true;
                return;
            }

            // 2. Нельзя менять роль администратора на более низкую
            if (columnHeader == "Роль" && user.IsAdmin)
            {
                var combo = e.EditingElement as ComboBox;
                int newRoleId = combo?.SelectedValue is int rid ? rid : -1;

                // RoleID == 1 соответствует администратору
                if (newRoleId != 1)
                {
                    MessageBox.Show(
                        "Нельзя понижать роль другого администратора.\n" +
                        "Сначала администратор должен сам сменить свою роль.",
                        "Запрет изменения роли",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    e.Cancel = true;
                    return;
                }
            }

            // Автосохранение: даём DataGrid завершить commit, потом сохраняем
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => _viewModel?.UpdateCommand?.Execute(null)));
        }

        // ══════════════════════════════════════════════════════════════════
        //  ЗАЩИТА ПРИ УДАЛЕНИИ
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается перед тем, как ViewModel выполнит DeleteCommand.
        /// Возвращает false, если удаление запрещено.
        /// </summary>
        public bool CanDeleteSelectedUser()
        {
            var user = _viewModel?.SelectedUser;
            if (user == null)
            {
                MessageBox.Show("Сначала выберите пользователя.", "Нет выбора",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            // Нельзя удалить себя
            if (user.UserID == _currentAdminId)
            {
                MessageBox.Show(
                    "Нельзя удалить собственную учётную запись.",
                    "Запрет удаления",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            // Нельзя удалить администратора
            if (user.IsAdmin)
            {
                MessageBox.Show(
                    $"Нельзя удалить пользователя «{user.Username}»: он является администратором.\n" +
                    "Сначала смените ему роль.",
                    "Запрет удаления",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            var result = MessageBox.Show(
                $"Вы уверены, что хотите удалить пользователя «{user.Username}»?\n" +
                "Это действие необратимо.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            return result == MessageBoxResult.Yes;
        }

        private void UpdateStatistics()
        {
            if (_viewModel?.SelectedUser == null)
            {
                if (_viewModel != null)
                {
                    _viewModel.SelectedUser = null;
                }
                return;
            }

            try
            {
                var stats = SqlDataAccess.GetTestStatisticsByUserId(
                    _viewModel.SelectedUser.UserID,
                    _currentCourseId
                );

                _viewModel.SelectedUser.TestStatistics.Clear();
                foreach (var stat in stats)
                {
                    _viewModel.SelectedUser.TestStatistics.Add(stat);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки статистики: {ex.Message}");
            }
        }
        private void DataGrid_SelectionChanged_1(object sender, SelectionChangedEventArgs e) { }
    }
}