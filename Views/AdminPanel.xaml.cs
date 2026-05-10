using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Computer_networks.Data;
using Computer_networks.Utilities;
using Computer_networks.ViewModels;
using Computer_networks.Models;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Text;

namespace Computer_networks.Views
{

    /// <summary>
    /// Модель строки в таблице студентов
    /// </summary>
    public class StudentStatRow
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public int TotalAttempts { get; set; }
        public double AvgScore { get; set; }
        public double BestScore { get; set; }
        public DateTime? LastActivity { get; set; }

        // Цвет строки в зависимости от успеваемости
        public string RowColor
        {
            get
            {
                if (AvgScore < 40) return "#FFE5E5"; // красный фон для отстающих
                if (AvgScore < 60) return "#FFF3CD"; // желтый фон для средних
                return "White"; // белый фон для хорошистов
            }
        }

        // Статус для отображения
        public string Status
        {
            get
            {
                if (AvgScore < 40) return "🔴 Отстающий";
                if (AvgScore < 60) return "🟡 Средний";
                if (AvgScore < 80) return "🟢 Хорошист";
                return "🏆 Отличник";
            }
        }
    }

    public partial class AdminPanel : UserControl
    {
        private List<StudentStatRow> _allStudents = new List<StudentStatRow>();
        private List<Group> _allGroups = new List<Group>();
        private AdminPanelViewModel _viewModel;
        private int _currentCourseId;
        private List<User> _allTeachers = new List<User>();
        private List<Course> _allCourses = new List<Course>();
        private ObservableCollection<TeacherCourseAssignment> _assignmentItems
            = new ObservableCollection<TeacherCourseAssignment>();

        public AdminPanel()
        {
            try
            {
                InitializeComponent();

                // Создаем ViewModel для биндинга
                _viewModel = new AdminPanelViewModel();
                this.DataContext = _viewModel;

                // Устанавливаем видимость кнопок по правам
                SetButtonVisibility();

                // Берём текущий курс из главного окна
                _currentCourseId = SqlDataAccess.CurrentCourseId;

                // Загружаем статистику
                LoadStatistics();

                // Загружаем группы
                LoadGroups();

                // Загружаем курсы
                LoadCourses();

                // Один раз привязываем таблицу назначений к ObservableCollection
                // (сама коллекция будет обновляться без переприсваивания ItemsSource)
                if (TeacherCourseAssignmentsList != null)
                    TeacherCourseAssignmentsList.ItemsSource = _assignmentItems;

                // Подписываемся на изменения
                this.Loaded += (s, e) => RefreshAllData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при инициализации панели: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ───────────────────────────────────────────────────────────────
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ───────────────────────────────────────────────────────────────

        private void SetButtonVisibility()
        {
            try
            {
                bool canManageUsers = UserManager.CanManageUsers();
                bool canManageContent = UserManager.CanManageContent();

                BtnManageUsers.Visibility = canManageUsers ? Visibility.Visible : Visibility.Collapsed;
                BtnManageContent.Visibility = canManageContent ? Visibility.Visible : Visibility.Collapsed;
                BtnManageTests.Visibility = canManageContent ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AdminPanel] Ошибка SetButtonVisibility: {ex.Message}");
            }
        }

        private void RefreshAllData()
        {
            LoadStatistics();
            LoadGroups();
            LoadTeacherCoursePanel();
        }

        // Вызывается из MainWindow при смене курса
        public void UpdateCourse(int courseId)
        {
            _currentCourseId = courseId;
            RefreshAllData();
        }

        // ───────────────────────────────────────────────────────────────
        // ЗАГРУЗКА СТАТИСТИКИ
        // ───────────────────────────────────────────────────────────────

        private void LoadStatistics()
        {
            try
            {
                LoadCourseSummary();
                LoadStudentStats();

                if (TxtLastUpdated != null)
                    TxtLastUpdated.Text = $"Обновлено: {DateTime.Now:dd.MM.yyyy HH:mm}";

                _viewModel?.RefreshRoleInfo();
            }
            catch (Exception ex)
            {
                if (TxtLastUpdated != null)
                    TxtLastUpdated.Text = $"Ошибка загрузки: {ex.Message}";

                System.Diagnostics.Debug.WriteLine($"[AdminPanel] Ошибка LoadStatistics: {ex.Message}");
            }
        }

        private void LoadCourseSummary()
        {
            try
            {
                using (var connection = new SqlConnection(SqlDataAccess.ConnectionString))
                {
                    // Количество студентов (роль 3)
                    int students = connection.QueryFirstOrDefault<int>(
                        "SELECT COUNT(*) FROM Users WHERE RoleID = 3");

                    // Количество тем в выбранном курсе
                    int topics = connection.QueryFirstOrDefault<int>(
                        "SELECT COUNT(*) FROM Topics WHERE CourseID = @CourseId",
                        new { CourseId = _currentCourseId });

                    // Количество вопросов в выбранном курсе
                    int questions = connection.QueryFirstOrDefault<int>(
                        "SELECT COUNT(*) FROM Questions q JOIN Topics t ON q.TopicID = t.TopicID WHERE t.CourseID = @CourseId",
                        new { CourseId = _currentCourseId });

                    // Средний балл по выбранному курсу
                    double avgScore = connection.QueryFirstOrDefault<double?>(@"
                        SELECT AVG(CAST(CorrectAnswers AS FLOAT) * 100.0 / TotalQuestions)
                        FROM TestResults tr
                        JOIN Topics t ON tr.TopicID = t.TopicID
                        WHERE t.CourseID = @CourseId
                          AND tr.TotalQuestions > 0",
                        new { CourseId = _currentCourseId }) ?? 0;

                    // Обновляем UI в главном потоке
                    Dispatcher.Invoke(() =>
                    {
                        TxtTotalStudents.Text = students.ToString();
                        TxtTotalTopics.Text = topics.ToString();
                        TxtTotalQuestions.Text = questions.ToString();
                        TxtAvgScore.Text = $"{avgScore:F1}%";
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AdminPanel] Ошибка LoadCourseSummary: {ex.Message}");
            }
        }

        private void LoadStudentStats()
        {
            try
            {
                using (var connection = new SqlConnection(SqlDataAccess.ConnectionString))
                {
                    string sql = @"
                        SELECT
                            u.UserID,
                            u.Username,
                            u.Email,
                            ISNULL(COUNT(tr.ResultID), 0) AS TotalAttempts,
                            ISNULL(AVG(CAST(tr.CorrectAnswers AS FLOAT) * 100.0 / tr.TotalQuestions), 0) AS AvgScore,
                            ISNULL(MAX(CAST(tr.CorrectAnswers AS FLOAT) * 100.0 / tr.TotalQuestions), 0) AS BestScore,
                            MAX(tr.TestDate) AS LastActivity
                        FROM Users u
                        LEFT JOIN TestResults tr ON tr.UserID = u.UserID
                        LEFT JOIN Topics t ON tr.TopicID = t.TopicID AND t.CourseID = @CourseId
                        WHERE u.RoleID = 3
                        GROUP BY u.UserID, u.Username, u.Email
                        ORDER BY AvgScore DESC, TotalAttempts DESC";

                    var result = connection.Query<StudentStatRow>(sql, new { CourseId = _currentCourseId }).ToList();

                    Dispatcher.Invoke(() =>
                    {
                        _allStudents = result;
                        StudentsDataGrid.ItemsSource = _allStudents;
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Ошибка загрузки статистики студентов:\n{ex.Message}",
                                    "Ошибка загрузки",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                });
            }
        }

        // ───────────────────────────────────────────────────────────────
        // ЗАГРУЗКА ГРУПП
        // ───────────────────────────────────────────────────────────────

        private void LoadGroups()
        {
            try
            {
                _allGroups = SqlDataAccess.GetAllGroups();
                // Цикл foreach убран — StudentCount заполняется прямо в GetAllGroups через COUNT в запросе

                Dispatcher.Invoke(() =>
                {
                    GroupsList.ItemsSource = _allGroups;
                    CompareGroupsList.ItemsSource = _allGroups;
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Ошибка загрузки групп:\n{ex.Message}",
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }
        // ───────────────────────────────────────────────────────────────
        // ОБРАБОТЧИКИ СОБЫТИЙ
        // ───────────────────────────────────────────────────────────────

        private void SearchStudentBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string query = SearchStudentBox.Text.Trim().ToLowerInvariant();

                if (string.IsNullOrEmpty(query) || _allStudents == null)
                {
                    StudentsDataGrid.ItemsSource = _allStudents;
                    return;
                }

                var filtered = _allStudents
                    .Where(s => s.Username.ToLowerInvariant().Contains(query)
                             || (s.Email?.ToLowerInvariant().Contains(query) == true))
                    .ToList();

                StudentsDataGrid.ItemsSource = filtered;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AdminPanel] Ошибка поиска: {ex.Message}");
            }
        }

        private void RefreshStats_Click(object sender, RoutedEventArgs e)
        {
            SearchStudentBox.Text = string.Empty;
            _currentCourseId = SqlDataAccess.CurrentCourseId;
            RefreshAllData();
        }

        private void StudentsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (StudentsDataGrid.SelectedItem is StudentStatRow student)
            {
                string details = $"👤 Студент: {student.Username}\n" +
                                $"📧 Email: {student.Email}\n" +
                                $"📊 Всего попыток: {student.TotalAttempts}\n" +
                                $"📈 Средний балл: {student.AvgScore:F1}%\n" +
                                $"🏆 Лучший балл: {student.BestScore:F1}%\n" +
                                $"🕐 Последняя активность: {student.LastActivity:dd.MM.yyyy HH:mm}\n" +
                                $"📌 Статус: {student.Status}";

                MessageBox.Show(details, "Детальная информация о студенте",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ───────────────────────────────────────────────────────────────
        // ОБРАБОТЧИКИ ДЛЯ ГРУПП
        // ───────────────────────────────────────────────────────────────

        private void CreateGroup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var editorWindow = new GroupEditorWindow(_currentCourseId);
                editorWindow.Owner = Window.GetWindow(this);

                if (editorWindow.ShowDialog() == true)
                {
                    LoadGroups();
                    TxtLastUpdated.Text = $"Группа создана: {DateTime.Now:dd.MM.yyyy HH:mm}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании группы:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshGroups_Click(object sender, RoutedEventArgs e)
        {
            LoadGroups();
            TxtLastUpdated.Text = $"Группы обновлены: {DateTime.Now:dd.MM.yyyy HH:mm}";
        }

        private void ViewGroupStats_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is int groupId)
                {
                    var statsWindow = new GroupStatsWindow(groupId, _currentCourseId);
                    statsWindow.Owner = Window.GetWindow(this);
                    statsWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при просмотре статистики группы:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditGroup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is int groupId)
                {
                    var group = _allGroups.FirstOrDefault(g => g.GroupID == groupId);
                    if (group != null)
                    {
                        var editorWindow = new GroupEditorWindow(group, _currentCourseId);
                        editorWindow.Owner = Window.GetWindow(this);

                        if (editorWindow.ShowDialog() == true)
                        {
                            LoadGroups();
                            TxtLastUpdated.Text = $"Группа обновлена: {DateTime.Now:dd.MM.yyyy HH:mm}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при редактировании группы:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is int groupId)
                {
                    var group = _allGroups.FirstOrDefault(g => g.GroupID == groupId);
                    if (group == null) return;

                    var result = MessageBox.Show(
                        $"Вы уверены, что хотите удалить группу \"{group.GroupName}\"?\n\n" +
                        $"В группе {group.StudentCount} студентов. Все связи будут удалены.",
                        "Подтверждение удаления",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        SqlDataAccess.DeleteGroup(groupId);
                        LoadGroups();
                        TxtLastUpdated.Text = $"Группа удалена: {DateTime.Now:dd.MM.yyyy HH:mm}";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении группы:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CompareGroups_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedGroups = new List<Group>();
                foreach (var item in CompareGroupsList.SelectedItems)
                {
                    if (item is Group group)
                        selectedGroups.Add(group);
                }

                if (selectedGroups.Count < 2)
                {
                    MessageBox.Show("Выберите минимум 2 группы для сравнения.",
                        "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Очищаем панель результатов
                ComparisonResultsPanel.Children.Clear();

                // Заголовок
                ComparisonResultsPanel.Children.Add(new TextBlock
                {
                    Text = $"Сравнение групп по курсу ID: {_currentCourseId}",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 15)
                });

                // Таблица сравнения
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                foreach (var group in selectedGroups)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                }

                // Заголовки
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Названия групп
                for (int i = 0; i < selectedGroups.Count; i++)
                {
                    var header = new TextBlock
                    {
                        Text = selectedGroups[i].GroupName,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(5),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    Grid.SetColumn(header, i + 1);
                    Grid.SetRow(header, 0);
                    grid.Children.Add(header);
                }

                // Загружаем статистику для каждой группы
                int row = 1;
                string[] metrics = new string[] { "Средний балл", "Всего попыток", "Изучено тем", "Тестов на 100%" };

                foreach (string metric in metrics)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    // Название метрики
                    var metricLabel = new TextBlock
                    {
                        Text = metric,
                        Margin = new Thickness(5),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(metricLabel, 0);
                    Grid.SetRow(metricLabel, row);
                    grid.Children.Add(metricLabel);

                    // Значения для каждой группы
                    for (int i = 0; i < selectedGroups.Count; i++)
                    {
                        var stats = SqlDataAccess.GetGroupStatistics(selectedGroups[i].GroupID, _currentCourseId);

                        string value = "";
                        if (metric == "Средний балл")
                            value = stats?.AvgScore.ToString("F1") + "%";
                        else if (metric == "Всего попыток")
                            value = stats?.TotalAttempts.ToString() ?? "0";
                        else if (metric == "Изучено тем")
                            value = stats?.CompletedTopics.ToString() ?? "0";
                        else if (metric == "Тестов на 100%")
                            value = stats?.PerfectTests.ToString() ?? "0";
                        else
                            value = "—";

                        var valueBlock = new TextBlock
                        {
                            Text = value,
                            Margin = new Thickness(5),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            FontWeight = FontWeights.SemiBold
                        };
                        Grid.SetColumn(valueBlock, i + 1);
                        Grid.SetRow(valueBlock, row);
                        grid.Children.Add(valueBlock);
                    }

                    row++;
                }

                ComparisonResultsPanel.Children.Add(grid);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сравнении групп:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ───────────────────────────────────────────────────────────────
        // ОТКРЫТИЕ ОКОН УПРАВЛЕНИЯ
        // ───────────────────────────────────────────────────────────────

        private Window GetParentWindow() => Window.GetWindow(this);

        private void OpenUsersWindow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!UserManager.CanManageUsers())
                {
                    MessageBox.Show("У вас нет прав для управления пользователями.",
                        "Доступ запрещён", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var w = new AdminUsersWindow { Owner = GetParentWindow() };
                w.ShowDialog();
                RefreshAllData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии окна пользователей: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenContentWindow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!UserManager.CanManageContent())
                {
                    MessageBox.Show("У вас нет прав для редактирования контента.",
                        "Доступ запрещён", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var w = new AdminContentWindow(_currentCourseId) { Owner = GetParentWindow() };
                w.ShowDialog();
                LoadCourseSummary();
                TxtLastUpdated.Text = $"Обновлено: {DateTime.Now:dd.MM.yyyy HH:mm}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии окна контента: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenTestsWindow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!UserManager.CanManageContent())
                {
                    MessageBox.Show("У вас нет прав для управления тестами.",
                        "Доступ запрещён", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var w = new AdminTestsWindow(_currentCourseId) { Owner = GetParentWindow() };
                w.ShowDialog();
                LoadCourseSummary();
                TxtLastUpdated.Text = $"Обновлено: {DateTime.Now:dd.MM.yyyy HH:mm}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии окна тестов: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Управление курсами ────────────────────────────────────────

        private void LoadCourses()
        {
            try
            {
                var courses = SqlDataAccess.GetAllCoursesIncludeInactive();
                CoursesListView.ItemsSource = courses;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AdminPanel] Ошибка загрузки курсов: {ex.Message}");
            }
        }

        private void CoursesListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Можно расширить в будущем
        }

        private void AddCourse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CourseEditDialog();
            dialog.Owner = GetParentWindow();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    int newId = SqlDataAccess.AddCourse(dialog.CourseName, dialog.CourseDescription);
                    LoadCourses();
                    LoadTeacherCoursePanel();

                    // Обновляем CourseSelector в главном окне
                    RefreshCourseSelectorInMainWindow();

                    MessageBox.Show($"Курс «{dialog.CourseName}» успешно добавлен (ID: {newId}).",
                        "Курс добавлен", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при добавлении курса: {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void EditCourse_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is Computer_networks.Models.Course course)
            {
                var dialog = new CourseEditDialog(course.CourseName, course.CourseDescription);
                dialog.Owner = GetParentWindow();
                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        SqlDataAccess.UpdateCourseInfo(course.CourseID, dialog.CourseName, dialog.CourseDescription);
                        LoadCourses();
                        LoadTeacherCoursePanel();
                        RefreshCourseSelectorInMainWindow();
                        MessageBox.Show($"Курс обновлён.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ToggleCourseActive_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is Computer_networks.Models.Course course)
            {
                bool newState = !course.IsActive;
                string action = newState ? "активировать" : "скрыть";

                var result = MessageBox.Show(
                    $"Вы хотите {action} курс «{course.CourseName}»?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        SqlDataAccess.SetCourseActive(course.CourseID, newState);
                        LoadCourses();
                        LoadTeacherCoursePanel();
                        RefreshCourseSelectorInMainWindow();

                        if (!newState)
                        {
                            var activeCourses = SqlDataAccess.GetAllCourses();
                            if (activeCourses.Count == 0)
                                MessageBox.Show(
                                    "Все курсы скрыты. Добавьте или активируйте хотя бы один курс.",
                                    "Нет активных курсов",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void RefreshCourseSelectorInMainWindow()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow == null) return;

                var courses = SqlDataAccess.GetAllCourses(); // только активные
                mainWindow.CourseSelector.ItemsSource = courses;

                if (courses.Count == 0) return;

                // Если текущий курс всё ещё активен — сохраняем выбор
                bool currentStillActive = courses.Any(c => c.CourseID == SqlDataAccess.CurrentCourseId);
                if (currentStillActive)
                    mainWindow.CourseSelector.SelectedValue = SqlDataAccess.CurrentCourseId;
                else
                    mainWindow.CourseSelector.SelectedValue = courses.First().CourseID;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AdminPanel] RefreshCourseSelector: {ex.Message}");
            }
        }
        private void LoadTeacherCoursePanel()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[LoadTeacherCoursePanel] Загрузка...");

                _allTeachers = SqlDataAccess.GetAllUsers()
                    ?.Where(u => u.RoleID == 2).ToList()
                    ?? new List<User>();
                _allCourses = SqlDataAccess.GetAllCourses() ?? new List<Course>();

                System.Diagnostics.Debug.WriteLine($"[LoadTeacherCoursePanel] Преподавателей: {_allTeachers.Count}, Курсов: {_allCourses.Count}");

                if (AdminTeacherSelector != null)
                {
                    // Сначала отписываемся, чтобы сброс ItemsSource не тригерил лишние события
                    AdminTeacherSelector.SelectionChanged -= AdminTeacherSelector_SelectionChanged;
                    AdminTeacherSelector.ItemsSource = null;
                    AdminTeacherSelector.ItemsSource = _allTeachers;
                    AdminTeacherSelector.SelectionChanged += AdminTeacherSelector_SelectionChanged;
                }

                if (AdminCourseForTeacher != null)
                {
                    AdminCourseForTeacher.ItemsSource = null;
                    AdminCourseForTeacher.ItemsSource = _allCourses;
                }

                RefreshTeacherCourseList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadTeacherCoursePanel] ОШИБКА: {ex.Message}");
            }
        }

        private void AdminTeacherSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshTeacherCourseList();
        }

        private void RefreshTeacherCourseList()
        {
            try
            {
                var assignments = SqlDataAccess.GetAllTeacherAssignments();
                System.Diagnostics.Debug.WriteLine($"[RefreshTeacherCourseList] Назначений: {assignments.Count}");

                _assignmentItems.Clear();
                foreach (var a in assignments)
                    _assignmentItems.Add(a);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RefreshTeacherCourseList] ОШИБКА: {ex.Message}");
            }
        }

        private void AssignTeacher_Click(object sender, RoutedEventArgs e)
        {
            var teacher = AdminTeacherSelector?.SelectedItem as User;
            var course = AdminCourseForTeacher?.SelectedItem as Course;

            if (teacher == null || course == null)
            {
                MessageBox.Show("Выберите преподавателя и курс.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SqlDataAccess.AssignTeacherToCourse(
                    teacher.UserID, course.CourseID, UserManager.GetCurrentUserId());
                RefreshTeacherCourseList();
                MessageBox.Show(
                    $"Преподаватель «{teacher.Username}» назначен на курс «{course.CourseName}».",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UnassignTeacher_Click(object sender, RoutedEventArgs e)
        {
            var assignment = (sender as Button)?.Tag as TeacherCourseAssignment;
            if (assignment == null)
            {
                MessageBox.Show("Не удалось определить назначение.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Снять «{assignment.TeacherName}» с курса «{assignment.CourseName}»?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                SqlDataAccess.UnassignTeacherFromCourse(assignment.TeacherID, assignment.CourseID);
                RefreshTeacherCourseList();
                MessageBox.Show(
                    $"Преподаватель «{assignment.TeacherName}» снят с курса «{assignment.CourseName}».",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}