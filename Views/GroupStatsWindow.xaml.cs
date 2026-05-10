using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Computer_networks.Data;
using Computer_networks.Models;

namespace Computer_networks.Views
{
    public partial class GroupStatsWindow : Window
    {
        private readonly int _groupId;
        private readonly int _courseId;

        public GroupStatsWindow(int groupId, int courseId)
        {
            InitializeComponent();
            _groupId = groupId;
            _courseId = courseId;

            LoadGroupInfo();
            LoadStatistics();
        }

        // ─── Загрузка названия группы ────────────────────────────────────
        private void LoadGroupInfo()
        {
            try
            {
                var group = SqlDataAccess.GetGroupById(_groupId);
                GroupNameText.Text = group?.GroupName ?? "Неизвестная группа";
            }
            catch (Exception ex)
            {
                GroupNameText.Text = "Ошибка";
                System.Diagnostics.Debug.WriteLine($"LoadGroupInfo: {ex.Message}");
            }
        }

        // ─── Загрузка статистики ─────────────────────────────────────────
        private void LoadStatistics()
        {
            try
            {
                var stats = SqlDataAccess.GetGroupStatistics(_groupId, _courseId);

                if (stats == null)
                {
                    MessageBox.Show("Не удалось загрузить статистику группы.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Карточки сводки
                TxtAvgScore.Text = $"{stats.AvgScore:F1}%";
                TxtTotalAttempts.Text = stats.TotalAttempts.ToString();
                TxtCompletedTopics.Text = stats.CompletedTopics.ToString();
                TxtPerfectTests.Text = stats.PerfectTests.ToString();

                // Прогресс по темам
                // ИСПРАВЛЕНО: убран MultiBinding с ActualWidth — он давал 0 при первом рендере.
                // Теперь используем простой ProgressBar со значением от 0 до 100.
                TopicsStatsList.ItemsSource = stats.TopicStats;

                // Сравнение с общим средним — добавляем цвет здесь, в code-behind
                if (stats.TopicStats != null)
                {
                    var comparisonData = stats.TopicStats.Select(t => new TopicComparisonRow
                    {
                        TopicTitle = t.TopicTitle,
                        GroupAvgScore = t.GroupAvgScore,
                        OverallAvgScore = t.OverallAvgScore,
                        // Зелёный — группа выше среднего, красный — ниже
                        CompareColor = t.GroupAvgScore >= t.OverallAvgScore
                            ? new SolidColorBrush(Color.FromRgb(40, 167, 69))
                            : new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                        // Разница со знаком для отображения рядом
                        DiffText = (t.GroupAvgScore - t.OverallAvgScore) >= 0
                            ? $"+{t.GroupAvgScore - t.OverallAvgScore:F1}%"
                            : $"{t.GroupAvgScore - t.OverallAvgScore:F1}%"
                    }).ToList();

                    ComparisonList.ItemsSource = comparisonData;
                }

                // Топ студентов
                TopStudentsGrid.ItemsSource = stats.TopStudents;

                UpdateTimeText.Text = $"Обновлено: {DateTime.Now:dd.MM.yyyy HH:mm}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки статистики:\n{ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Кнопка обновить ─────────────────────────────────────────────
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadStatistics();
        }

        // ─── Управление окном ────────────────────────────────────────────
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
            if (e.Key == Key.F5) LoadStatistics();
        }
    }

    /// <summary>
    /// Строка таблицы сравнения — выделена отдельно, чтобы XAML мог биндиться на DiffText и CompareColor.
    /// </summary>
    public class TopicComparisonRow
    {
        public string TopicTitle { get; set; }
        public double GroupAvgScore { get; set; }
        public double OverallAvgScore { get; set; }
        public SolidColorBrush CompareColor { get; set; }
        public string DiffText { get; set; }
    }
}
