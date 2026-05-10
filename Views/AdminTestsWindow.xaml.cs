using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Computer_networks.Data;
using Computer_networks.Models;
using System.ComponentModel;
using System.Windows.Data;
using System.Globalization;

namespace Computer_networks.Views
{
    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isValid) { return isValid ? 1.0 : 0.5; }
            return 1.0;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class AdminTestsWindow : Window, INotifyPropertyChanged
    {
        public event Action DataUpdated;
        private ObservableCollection<Topic> _topics;
        private Topic _selectedTopic;
        private Question _selectedQuestion;
        private ObservableCollection<Question> _currentQuestions;
        private int _currentCourseId;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public Topic SelectedTopic
        {
            get => _selectedTopic;
            set
            {
                _selectedTopic = value;
                OnPropertyChanged(nameof(SelectedTopic));
            }
        }

        public Question SelectedQuestion
        {
            get => _selectedQuestion;
            set
            {
                _selectedQuestion = value;
                OnPropertyChanged(nameof(SelectedQuestion));
                if (value != null)
                {
                    BindQuestionToEditor();
                }
            }
        }

        public AdminTestsWindow(int courseId)
        {
            InitializeComponent();
            _currentCourseId = courseId;
            // ✅ Синхронизируем статическое поле при открытии окна
            SqlDataAccess.CurrentCourseId = courseId;
            this.DataContext = this;
            LoadCourses();
            LoadTopics();
            KeyDown += Window_KeyDown;
        }

        public AdminTestsWindow() : this(2) { }

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

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
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

        private void CourseSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CourseSelector.SelectedValue is int courseId && courseId != _currentCourseId)
            {
                _currentCourseId = courseId;

                // ✅ ГЛАВНОЕ ИСПРАВЛЕНИЕ: обновляем статическое поле в SqlDataAccess.
                // Именно оно используется как fallback в GetQuestionsByTopicId и других методах,
                // когда courseId передаётся как null. Без этого запросы всегда шли по CourseID = 2.
                SqlDataAccess.CurrentCourseId = courseId;

                // Очищаем все данные
                _topics?.Clear();
                _currentQuestions?.Clear();
                TopicsListView.ItemsSource = null;
                QuestionsListView.ItemsSource = null;
                OptionsListView.ItemsSource = null;
                QuestionTextTextBox.Text = string.Empty;

                // Загружаем темы нового курса
                LoadTopics();
            }
        }

        private void LoadTopics()
        {
            try
            {
                var allTopics = SqlDataAccess.GetAllTopics(_currentCourseId);
                _topics = new ObservableCollection<Topic>(allTopics);
                TopicsListView.ItemsSource = _topics;

                if (_topics.Any())
                {
                    TopicsListView.SelectedItem = _topics.First();
                    SelectedTopic = _topics.First();
                    LoadQuestionsForSelectedTopic();
                }
                else
                {
                    _currentQuestions = new ObservableCollection<Question>();
                    QuestionsListView.ItemsSource = _currentQuestions;
                    OptionsListView.ItemsSource = null;
                    QuestionTextTextBox.Text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки тем: {ex.Message}");
            }
        }

        private void TopicsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TopicsListView.SelectedItem is Topic selectedTopic)
            {
                SelectedTopic = selectedTopic;
                LoadQuestionsForSelectedTopic();
            }
        }

        private void LoadQuestionsForSelectedTopic()
        {
            OptionsListView.ItemsSource = null;
            QuestionTextTextBox.Text = string.Empty;

            if (SelectedTopic == null)
            {
                _currentQuestions = new ObservableCollection<Question>();
                QuestionsListView.ItemsSource = _currentQuestions;
                return;
            }

            try
            {
                // ✅ Передаём _currentCourseId явно — не полагаемся на статическое поле
                var questions = SqlDataAccess.GetQuestionsByTopicId(SelectedTopic.TopicID, _currentCourseId);
                _currentQuestions = new ObservableCollection<Question>(questions);
                QuestionsListView.ItemsSource = _currentQuestions;

                if (_currentQuestions.Any())
                {
                    QuestionsListView.SelectedItem = _currentQuestions.First();
                    SelectedQuestion = _currentQuestions.First();
                }
                else
                {
                    SelectedQuestion = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки вопросов: {ex.Message}");
                _currentQuestions = new ObservableCollection<Question>();
                QuestionsListView.ItemsSource = _currentQuestions;
            }
        }

        private void QuestionsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QuestionsListView.SelectedItem is Question selectedQuestion)
            {
                SelectedQuestion = selectedQuestion;
            }
        }

        private void BindQuestionToEditor()
        {
            if (SelectedQuestion != null)
            {
                QuestionTextTextBox.Text = SelectedQuestion.Text;
                OptionsListView.ItemsSource = new ObservableCollection<Answer>(SelectedQuestion.Answers);
            }
            else
            {
                QuestionTextTextBox.Text = string.Empty;
                OptionsListView.ItemsSource = null;
            }
        }

        private void NewQuestionButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTopic == null)
            {
                MessageBox.Show("Сначала выберите тему, к которой будет привязан вопрос.");
                return;
            }

            var newQuestion = new Question
            {
                Text = "Новый Вопрос",
                TopicID = SelectedTopic.TopicID,
                QuestionID = 0,
                CourseID = _currentCourseId,
                QuestionType = "single",
                Difficulty = 3,
                Answers = new List<Answer>
                {
                    new Answer { Text = "Вариант 1", IsCorrect = true, AnswerID = 0 },
                    new Answer { Text = "Вариант 2", IsCorrect = false, AnswerID = 0 }
                }
            };

            _currentQuestions.Add(newQuestion);
            QuestionsListView.SelectedItem = newQuestion;
            DataUpdated?.Invoke();
        }

        private void DeleteQuestionButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedQuestion == null) return;

            var result = MessageBox.Show($"Удалить вопрос: '{SelectedQuestion.Text}'?", "Подтверждение", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                _currentQuestions.Remove(SelectedQuestion);
                SelectedQuestion = _currentQuestions.FirstOrDefault();
                DataUpdated?.Invoke();
            }
        }

        private void AddOptionButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedQuestion == null) return;

            if (OptionsListView.ItemsSource is ObservableCollection<Answer> answers)
            {
                answers.Add(new Answer { Text = "Новый Вариант", IsCorrect = false, AnswerID = 0 });
            }
        }

        private void DeleteOptionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Answer answer)
            {
                if (OptionsListView.ItemsSource is ObservableCollection<Answer> answers)
                {
                    if (answers.Count <= 2)
                    {
                        MessageBox.Show("Должно остаться хотя бы 2 варианта ответа", "Внимание",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    answers.Remove(answer);
                }
            }
        }

        private void IsCorrectCheckBox_Click(object sender, RoutedEventArgs e)
        {
            var currentAnswer = (sender as CheckBox)?.DataContext as Answer;
            if (currentAnswer != null && currentAnswer.IsCorrect)
            {
                foreach (var answer in OptionsListView.ItemsSource.OfType<Answer>())
                {
                    if (answer != currentAnswer)
                    {
                        answer.IsCorrect = false;
                    }
                }
                OptionsListView.Items.Refresh();
            }
        }

        private void SaveAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTopic == null)
            {
                MessageBox.Show("Нет выбранной темы для сохранения вопросов.");
                return;
            }

            if (SelectedQuestion != null)
            {
                SelectedQuestion.Text = QuestionTextTextBox.Text;

                // ✅ Синхронизируем варианты ответов из ListView обратно в вопрос перед сохранением
                if (OptionsListView.ItemsSource is ObservableCollection<Answer> currentAnswers)
                {
                    SelectedQuestion.Answers = currentAnswers.ToList();
                }
            }

            if (!ValidateQuestionsBeforeSave())
                return;

            try
            {
                SqlDataAccess.SaveAllQuestionsForTopic(SelectedTopic.TopicID, _currentQuestions.ToList());
                MessageBox.Show("Все вопросы и ответы сохранены в базе данных!");
                DataUpdated?.Invoke();
                LoadQuestionsForSelectedTopic();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критическая ошибка сохранения: {ex.Message}");
            }
        }

        private bool ValidateQuestionsBeforeSave()
        {
            foreach (var question in _currentQuestions)
            {
                if (string.IsNullOrWhiteSpace(question.Text))
                {
                    MessageBox.Show("Все вопросы должны содержать текст", "Ошибка валидации",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                if (question.Answers == null || question.Answers.Count < 2)
                {
                    MessageBox.Show($"Вопрос \"{question.Text}\" должен иметь хотя бы 2 варианта ответа",
                        "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                if (question.Answers.All(a => !a.IsCorrect))
                {
                    MessageBox.Show($"Вопрос \"{question.Text}\" должен иметь хотя бы 1 правильный ответ",
                        "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }

            return true;
        }

        private void EditTopicContentButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTopic != null)
            {
                var adminContentWindow = new AdminContentWindow(_currentCourseId);
                adminContentWindow.Owner = this;
                adminContentWindow.ShowDialog();
                LoadTopics();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}