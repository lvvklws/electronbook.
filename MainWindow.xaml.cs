using Computer_networks.Data;
using Computer_networks.Models;
using Computer_networks.Services;
using Computer_networks.Utilities;
using Computer_networks.ViewModels;
using Computer_networks.Views;
using Dapper;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;



namespace Computer_networks
{
    public partial class MainWindow : Window
    {
        private List<Topic> allTopics = new List<Topic>();
        private Topic currentTopic;
        private List<Topic> linearizedTopics = new List<Topic>();
        private int currentTopicIndex = -1;
        //private User currentUser;
        private TestOption selectedTestOption;

        // private bool _isUserLoggedIn = false;
        // Управление размером шрифта
        private double currentFontSize = 14;
        private readonly double[] fontSizes = { 14, 17, 20 };
        private int currentSizeIndex = 0;

        // *** НОВЫЕ ПОЛЯ ДЛЯ ТЕСТИРОВАНИЯ ***
        private List<Question> currentTestQuestions = new List<Question>(); // <-- Убедитесь, что это есть
        private int currentQuestionIndex = 0;
        private int correctAnswersCount = 0;
        private bool isTestInProgress = false;
        private bool isFeedbackMode = false;

        private readonly SolidColorBrush DefaultBorderColor = new SolidColorBrush(Colors.Gray);

        private List<TestOption> _allTestOptions;



        private Dictionary<int, List<Question>> _questionsCache = new Dictionary<int, List<Question>>();

        private Dictionary<int, int> _questionCountCache = new Dictionary<int, int>();

        private Dictionary<string, List<Topic>> _searchCache = new Dictionary<string, List<Topic>>();

        private DateTime _lastCacheUpdate = DateTime.Now;

        private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(30);

        private bool _isUpdating = false;

        private readonly object _updateLock = new object();

        private DispatcherTimer _searchTimer;

        private string _lastSearchText = "";

        private List<Topic> _lastFilteredTopics;

        private static readonly SolidColorBrush DefaultBorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224));
        private static readonly SolidColorBrush SelectedBorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
        private static readonly SolidColorBrush CorrectBrush = new SolidColorBrush(Color.FromArgb(30, 0, 122, 204));
        private static readonly SolidColorBrush IncorrectBrush = new SolidColorBrush(Color.FromArgb(30, 255, 68, 68));
        private static readonly SolidColorBrush LightGrayBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240));
        private static readonly SolidColorBrush GrayBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200));
        private static readonly SolidColorBrush DarkGrayBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100));
        private static readonly SolidColorBrush DarkBlueBrush = new SolidColorBrush(Color.FromRgb(44, 62, 80));
        private List<LabWork> _currentCourseLabs = new List<LabWork>();
        private List<LabReportSubmission> _currentUserLabReports = new List<LabReportSubmission>();
        private List<Course> _teacherCourses = new List<Course>();
        private int _selectedLabCourseId = -1;

        // ===== МЕТОД ДЛЯ ОЧИСТКИ КЭША =====
        private void ClearCache()
        {
            _questionsCache.Clear();
            _questionCountCache.Clear();
            _searchCache.Clear();
            _lastFilteredTopics = null;
            _lastCacheUpdate = DateTime.MinValue;
            _allTestOptions = null;
            // #11 ИСПРАВЛЕНО: вместо null присваиваем пустой список,
            // чтобы последующие вызовы .OrderBy() не бросали NullReferenceException
            allTopics = new List<Topic>();
        }

        public MainWindow()
        {
            SetBrowserEmulationMode(); // ← ЭТО ДОЛЖНО БЫТЬ ПЕРВЫМ, до InitializeComponent!
            InitializeComponent();

            LoadTopicsAndBuildTree();
            PopulateTestSelectionListBox();
            UpdateFontSizeButtons();
            DataMessenger.DataChanged += HandleDataUpdate;
            SqlDataAccess.EnsureDbSchema();
            LoadCourses();
            UpdateUserInterface();
            LoadLabsData();

        }


        // #19 ИСПРАВЛЕНО: HtmlToWpf защищён от вложенных тегов
        // Добавлена поддержка <br>, <strong>, <em>, <img alt>
        // TODO (след. версия): заменить на HtmlAgilityPack для полной надёжности
        private List<UIElement> HtmlToWpf(string html, double fontSize)
        {
            var result = new List<UIElement>();
            if (string.IsNullOrWhiteSpace(html)) return result;

            try
            {
                var rxOpt = System.Text.RegularExpressions.RegexOptions.IgnoreCase
                          | System.Text.RegularExpressions.RegexOptions.Singleline;

                // Убираем script / style целиком
                html = System.Text.RegularExpressions.Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", rxOpt);
                html = System.Text.RegularExpressions.Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", rxOpt);

                // Нормализуем переносы строк
                html = System.Text.RegularExpressions.Regex.Replace(html, @"<br\s*/?>", "\n", rxOpt);

                // Вспомогательная функция: убирает ВСЕ вложенные теги рекурсивно
                string StripTags(string s) =>
                    System.Net.WebUtility.HtmlDecode(
                        System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "").Trim());

                // Вспомогательная функция: создаёт TextBlock с поддержкой <b>/<strong> и <em>/<i>
                TextBlock MakeRichText(string rawHtml, double fs, SolidColorBrush fg, Thickness margin = default)
                {
                    var tb = new TextBlock
                    {
                        FontSize = fs,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = fg,
                        Margin = margin
                    };
                    // Разбиваем на сегменты по <b>, <strong>, <em>, <i>
                    var segPattern = new System.Text.RegularExpressions.Regex(
                        @"<(strong|b|em|i)[^>]*>([\s\S]*?)</\1>|([^<]+)", rxOpt);
                    foreach (System.Text.RegularExpressions.Match m in segPattern.Matches(rawHtml))
                    {
                        string segTag = m.Groups[1].Value.ToLower();
                        string segText = m.Groups[2].Success ? StripTags(m.Groups[2].Value)
                                                             : System.Net.WebUtility.HtmlDecode(m.Groups[3].Value);
                        if (string.IsNullOrEmpty(segText)) continue;
                        var run = new Run(segText);
                        if (segTag == "strong" || segTag == "b") run.FontWeight = FontWeights.Bold;
                        if (segTag == "em" || segTag == "i") run.FontStyle = FontStyles.Italic;
                        tb.Inlines.Add(run);
                    }
                    if (!tb.Inlines.Any()) tb.Text = StripTags(rawHtml);
                    return tb;
                }

                // Главный парсер блоков — теперь с нежадным совпадением на уровне одного вложения
                var blockPat = new System.Text.RegularExpressions.Regex(
                    @"<(h[1-6]|p|ul|ol|pre|code|div|blockquote|table)[^>]*>([\s\S]*?)</\1>|([^<]+)",
                    rxOpt);

                foreach (System.Text.RegularExpressions.Match match in blockPat.Matches(html))
                {
                    string tag = match.Groups[1].Value.ToLower();
                    string inner = match.Groups[2].Value;
                    string plain = match.Groups[3].Value.Trim();

                    if (tag == "h1" || tag == "h2" || tag == "h3" || tag == "h4")
                    {
                        string cleanH = StripTags(inner);
                        if (string.IsNullOrWhiteSpace(cleanH)) continue;
                        double hSize = tag == "h1" ? fontSize + 8 :
                                       tag == "h2" ? fontSize + 5 :
                                       tag == "h3" ? fontSize + 3 : fontSize + 1;
                        result.Add(new TextBlock
                        {
                            Text = cleanH,
                            FontSize = Math.Min(hSize, 32),
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 12, 0, 6)
                        });
                    }
                    else if (tag == "p" || tag == "div")
                    {
                        if (string.IsNullOrWhiteSpace(inner)) continue;
                        var tb = MakeRichText(inner, fontSize,
                            new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                            new Thickness(0, 4, 0, 4));
                        tb.LineHeight = fontSize * 1.6;
                        result.Add(tb);
                    }
                    else if (tag == "ul" || tag == "ol")
                    {
                        // #19: Безопасно извлекаем <li>, поддерживая вложенные теги внутри пункта
                        var liPat = new System.Text.RegularExpressions.Regex(
                            @"<li[^>]*>([\s\S]*?)</li>", rxOpt);
                        var listPanel = new StackPanel { Margin = new Thickness(20, 4, 0, 4) };
                        int idx = 1;
                        foreach (System.Text.RegularExpressions.Match li in liPat.Matches(inner))
                        {
                            string liRaw = li.Groups[1].Value;
                            string bullet = tag == "ol" ? $"{idx}." : "•";
                            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                            row.Children.Add(new TextBlock
                            {
                                Text = bullet + " ",
                                FontSize = fontSize,
                                Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                                MinWidth = 20
                            });
                            row.Children.Add(MakeRichText(liRaw, fontSize,
                                new SolidColorBrush(Color.FromRgb(44, 62, 80))));
                            listPanel.Children.Add(row);
                            idx++;
                        }
                        result.Add(listPanel);
                    }
                    else if (tag == "pre" || tag == "code")
                    {
                        string code = StripTags(inner);
                        if (string.IsNullOrWhiteSpace(code)) continue;
                        var b = new Border
                        {
                            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                            CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(15),
                            Margin = new Thickness(0, 8, 0, 8)
                        };
                        b.Child = new TextBlock
                        {
                            Text = code,
                            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                            FontSize = fontSize - 1,
                            Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212)),
                            TextWrapping = TextWrapping.Wrap
                        };
                        result.Add(b);
                    }
                    else if (tag == "blockquote")
                    {
                        string q = StripTags(inner);
                        if (string.IsNullOrWhiteSpace(q)) continue;
                        var b = new Border
                        {
                            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                            BorderThickness = new Thickness(4, 0, 0, 0),
                            Padding = new Thickness(12, 6, 6, 6),
                            Margin = new Thickness(0, 6, 0, 6),
                            Background = new SolidColorBrush(Color.FromRgb(240, 247, 255))
                        };
                        b.Child = new TextBlock
                        {
                            Text = q,
                            FontSize = fontSize,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                            FontStyle = FontStyles.Italic
                        };
                        result.Add(b);
                    }
                    else if (!string.IsNullOrWhiteSpace(plain))
                    {
                        result.Add(new TextBlock
                        {
                            Text = System.Net.WebUtility.HtmlDecode(plain),
                            FontSize = fontSize,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                            Margin = new Thickness(0, 2, 0, 2)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback — показываем голый текст без тегов
                result.Add(new TextBlock
                {
                    Text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", ""),
                    FontSize = fontSize,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80))
                });
                System.Diagnostics.Debug.WriteLine($"[HtmlToWpf] Ошибка: {ex.Message}");
            }

            return result;
        }

        private void LoadCourses()
        {
            var courses = SqlDataAccess.GetAllCourses();
            CourseSelector.ItemsSource = courses;
            CourseSelector.SelectedValue = SqlDataAccess.CurrentCourseId;
        }

        // ----------------------------------------------------------------------
        // МЕТОДЫ ЗАГРУЗКИ ДАННЫХ И ПОСТРОЕНИЯ ДЕРЕВА
        // ----------------------------------------------------------------------

        private void PopulateTestSelectionListBox()
        {
            if (TestSelectionListBox == null) return;

            // #12 ИСПРАВЛЕНО: проверка _isUpdating перемещена ВНУТРЬ lock,
            // чтобы два потока не могли одновременно пройти проверку и войти в блок
            if (_allTestOptions != null && DateTime.Now - _lastCacheUpdate < _cacheDuration)
            {
                TestSelectionListBox.ItemsSource = _allTestOptions;
                return;
            }

            lock (_updateLock)
            {
                // Повторная проверка уже внутри блокировки
                if (_isUpdating) return;
                try
                {
                    _isUpdating = true;

                    // Всегда загружаем темы текущего курса (они могут отличаться)
                    allTopics = SqlDataAccess.GetAllTopics();

                    // Создаем новый список тестов
                    _allTestOptions = new List<TestOption>();

                    // Добавляем Общий тест (используем кэшированное количество)
                    // ИСПРАВЛЕНО: используем явную проверку вместо GetValueOrDefault
                    int totalQuestions = 0;
                    if (_questionCountCache.ContainsKey(0))
                    {
                        totalQuestions = _questionCountCache[0];
                    }
                    else
                    {
                        // Если в кэше нет, загружаем
                        totalQuestions = SqlDataAccess.GetQuestionCountByTopicId(null);
                        _questionCountCache[0] = totalQuestions;
                    }

                    _allTestOptions.Add(new TestOption
                    {
                        TopicID = null,
                        Title = "Общий тест (Все темы)",
                        QuestionCount = totalQuestions,
                        IsBlocked = false,
                        BlockMessage = string.Empty
                    });

                    // Если пользователь авторизован - проверяем блокировки (только для студентов)
                    if (UserManager.IsLoggedIn)
                    {
                        int userId = UserManager.GetCurrentUserId();

                        // Блокировка применяется только к студентам
                        var blockedTopics = UserManager.CurrentUser.IsStudent
                            ? GetBlockedTopicsForUser(userId)
                            : new HashSet<int>();

                        // Добавляем тесты по каждой теме
                        foreach (var topic in allTopics.OrderBy(t => t.OrderIndex))
                        {
                            // ИСПРАВЛЕНО: используем явную проверку
                            int count = 0;
                            if (_questionCountCache.ContainsKey(topic.TopicID))
                            {
                                count = _questionCountCache[topic.TopicID];
                            }
                            else
                            {
                                // Если нет в кэше - загружаем
                                count = SqlDataAccess.GetQuestionCountByTopicId(topic.TopicID);
                                _questionCountCache[topic.TopicID] = count;
                            }

                            if (count > 0)
                            {
                                var testOption = new TestOption
                                {
                                    TopicID = topic.TopicID,
                                    Title = $"Тест по теме {topic.OrderIndex}. {topic.Title}",
                                    QuestionCount = count
                                };

                                // Проверяем блокировку
                                if (blockedTopics.Contains(topic.TopicID))
                                {
                                    testOption.IsBlocked = true;
                                    testOption.BlockMessage = $"Требуется повторение! (3 провала < 50%). Для разблокировки просмотрите тему '{topic.Title}'.";
                                }

                                _allTestOptions.Add(testOption);
                            }
                        }
                    }
                    else
                    {
                        // Для неавторизованных пользователей - просто добавляем все темы
                        foreach (var topic in allTopics.OrderBy(t => t.OrderIndex))
                        {
                            // ИСПРАВЛЕНО: используем явную проверку
                            int count = 0;
                            if (_questionCountCache.ContainsKey(topic.TopicID))
                            {
                                count = _questionCountCache[topic.TopicID];
                            }
                            else
                            {
                                count = SqlDataAccess.GetQuestionCountByTopicId(topic.TopicID);
                                _questionCountCache[topic.TopicID] = count;
                            }

                            if (count > 0)
                            {
                                _allTestOptions.Add(new TestOption
                                {
                                    TopicID = topic.TopicID,
                                    Title = $"Тест по теме {topic.OrderIndex}. {topic.Title}",
                                    QuestionCount = count
                                });
                            }
                        }
                    }

                    // Обновляем ItemsSource
                    TestSelectionListBox.ItemsSource = _allTestOptions;

                    // Выбираем первый элемент если есть
                    if (_allTestOptions.Any())
                    {
                        TestSelectionListBox.SelectedIndex = 0;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке списка тестов: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }

        // Вспомогательный метод для загрузки всех заблокированных тем одним запросом
        private HashSet<int> GetBlockedTopicsForUser(int userId)
        {
            try
            {
                using (var connection = new SqlConnection(SqlDataAccess.ConnectionString))
                {
                    var sql = @"
                SELECT t.TopicID
                FROM TestResults t
                WHERE t.UserID = @UserId
                AND t.TopicID IS NOT NULL
                GROUP BY t.TopicID
                HAVING COUNT(CASE WHEN CAST(t.CorrectAnswers AS FLOAT) / t.TotalQuestions < 0.5 THEN 1 END) >= 3
                AND NOT EXISTS (
                    SELECT 1 FROM UserProgress p 
                    WHERE p.UserID = @UserId 
                    AND p.TopicID = t.TopicID 
                    AND p.LastReviewDate > DATEADD(day, -1, GETDATE())
                )";

                    var result = connection.Query<int>(sql, new { UserId = userId }).ToList();
                    return new HashSet<int>(result);
                }
            }
            catch
            {
                return new HashSet<int>();
            }
        }

        private void TestSelectionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TestSelectionListBox.SelectedItem is TestOption selectedTest)
            {
                if (selectedTest.IsBlocked)
                {
                    // Тест заблокирован
                    StartTestButton.IsEnabled = false;

                    SelectedTestStatusText.Text = $"🚫 {selectedTest.DisplayTitle}. {selectedTest.BlockMessage}";
                }
                else
                {
                    // Тест доступен
                    StartTestButton.IsEnabled = true;
                    SelectedTestStatusText.Text = $"Выбран тест: {selectedTest.Title}. Доступно вопросов: {selectedTest.QuestionCount}";
                }
            }
            else
            {
                // Ничего не выбрано
                StartTestButton.IsEnabled = false;
                SelectedTestStatusText.Text = "Выберите тест для начала.";
            }
        }

        private void StartTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(TestSelectionListBox.SelectedItem is TestOption selectedTest))
            {
                MessageBox.Show("Пожалуйста, выберите тест перед началом.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!UserManager.IsLoggedIn)
            {
                MessageBox.Show("Для начала тестирования необходимо войти в систему.", "Авторизация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Проверка блокировки...
            if (selectedTest.TopicID.HasValue && selectedTest.TopicID.Value > 0)
            {
                int topicId = selectedTest.TopicID.Value;
                int userId = UserManager.GetCurrentUserId();

                bool isBlocked = SqlDataAccess.ShouldBlockTest(userId, topicId);
                bool reviewCompleted = SqlDataAccess.HasUserCompletedReviewAfterBlockTrigger(userId, topicId);

                if (isBlocked && !reviewCompleted)
                {
                    MessageBox.Show(
                        $"⛔ Тест по теме '{selectedTest.Title}' заблокирован. Вы набрали менее 50% в последних трех попытках. Для снятия блокировки перейдите на вкладку 'Главная' и **внимательно просмотрите контент этой темы**.",
                        "Требуется Повторение Материала",
                        MessageBoxButton.OK,
                        MessageBoxImage.Stop);

                    if (MainTabControl.Items.Count > 0)
                    {
                        MainTabControl.SelectedIndex = 0;
                    }
                    return;
                }
            }

            try
            {
                selectedTestOption = selectedTest;
                currentQuestionIndex = 0;
                correctAnswersCount = 0;

                int topicIdToPass = selectedTest.TopicID ?? 0;

                // ===== ОПТИМИЗАЦИЯ: Проверяем кэш =====
                if (_questionsCache.ContainsKey(topicIdToPass))
                {
                    currentTestQuestions = _questionsCache[topicIdToPass];
                }
                else
                {
                    // Загружаем вопросы с ответами одним запросом
                    currentTestQuestions = LoadQuestionsWithAnswers(topicIdToPass);

                    // Сохраняем в кэш
                    _questionsCache[topicIdToPass] = currentTestQuestions;
                }

                if (currentTestQuestions == null || !currentTestQuestions.Any())
                {
                    MessageBox.Show($"Вопросы для '{selectedTest.Title}' не найдены...", "Ошибка данных", MessageBoxButton.OK, MessageBoxImage.Information);
                    isTestInProgress = false;
                    return;
                }

                // Перемешиваем ответы для каждого вопроса
                foreach (var q in currentTestQuestions)
                {
                    if (q.Answers != null)
                    {
                        q.Answers = q.Answers.OrderBy(a => Guid.NewGuid()).ToList();
                    }
                }

                isTestInProgress = true;
                isFeedbackMode = false;

                // Скрываем приветствие и показываем панель вопросов
                WelcomePanel.Visibility = Visibility.Collapsed;
                TestSelectionPanel.Visibility = Visibility.Collapsed;
                TestResultPanel.Visibility = Visibility.Collapsed;
                QuestionDisplayPanel.Visibility = Visibility.Visible;

                DisplayCurrentQuestion();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критическая ошибка при запуске теста: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                isTestInProgress = false;
            }
        }

        // Новый метод для загрузки вопросов с ответами одним запросом
        private List<Question> LoadQuestionsWithAnswers(int topicId)
        {
            using (var connection = new SqlConnection(SqlDataAccess.ConnectionString))
            {
                string questionSql;
                string answerSql;
                object parameters;

                if (topicId == 0)
                {
                    questionSql = @"SELECT q.QuestionID, q.Text, q.TopicID, q.CourseID
                            FROM Questions q WHERE q.CourseID = @CourseId";
                    answerSql = @"SELECT a.AnswerID, a.QuestionID, a.Text AS Text, a.IsCorrect
                          FROM Answers a JOIN Questions q ON a.QuestionID = q.QuestionID
                          WHERE q.CourseID = @CourseId";
                    parameters = new { CourseId = SqlDataAccess.CurrentCourseId };
                }
                else
                {
                    questionSql = @"SELECT q.QuestionID, q.Text, q.TopicID, q.CourseID
                            FROM Questions q WHERE q.TopicID = @TopicId";
                    answerSql = @"SELECT a.AnswerID, a.QuestionID, a.Text AS Text, a.IsCorrect
                          FROM Answers a JOIN Questions q ON a.QuestionID = q.QuestionID
                          WHERE q.TopicID = @TopicId";
                    parameters = new { TopicId = topicId };
                }

                var questions = connection.Query<Question>(questionSql, parameters).ToList();
                var answers = connection.Query<Answer>(answerSql, parameters).ToList();

                foreach (var q in questions)
                    q.Answers = answers.Where(a => a.QuestionID == q.QuestionID).ToList();

                return questions;
            }
        }
        private void ExitTestButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Вы уверены, что хотите прервать текущий тест? Ваш прогресс будет потерян.",
                "Прервать тестирование",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                isTestInProgress = false;
                currentTestQuestions = null;
                currentQuestionIndex = 0;
                correctAnswersCount = 0;
                isFeedbackMode = false;

                // 👇 Возвращаем приветствие
                WelcomePanel.Visibility = Visibility.Visible;
                TestSelectionPanel.Visibility = Visibility.Visible;
                QuestionDisplayPanel.Visibility = Visibility.Collapsed;
                TestResultPanel.Visibility = Visibility.Collapsed;

                QuestionText.Text = "Здесь будет текст вопроса...";
                QuestionNumberText.Text = "Вопрос 0 из 0";
                AnswersPanel.Children.Clear();
            }
        }


        // Обработчик выбора варианта ответа
        private void AnswerRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (NextQuestionButton != null && !isFeedbackMode)
            {
                NextQuestionButton.IsEnabled = true;
            }
        }
        private void NextQuestionButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentTestQuestions == null || !currentTestQuestions.Any()) return;

            // Режим просмотра результата - переходим к следующему вопросу
            if (isFeedbackMode)
            {
                isFeedbackMode = false;
                currentQuestionIndex++;
                DisplayCurrentQuestion();
                return;
            }

            // Находим выбранный RadioButton
            RadioButton selectedRadioButton = null;
            Border selectedBorder = null;
            Border selectedNumberBorder = null;
            TextBlock selectedNumberText = null;

            foreach (StackPanel panel in AnswersPanel.Children.OfType<StackPanel>())
            {
                RadioButton rb = panel.Children.OfType<RadioButton>().FirstOrDefault();
                Border container = panel.Children.OfType<Border>().FirstOrDefault();

                if (rb?.IsChecked == true)
                {
                    selectedRadioButton = rb;
                    selectedBorder = container;
                    if (container.Child is Grid grid && grid.Children[0] is Border numBorder)
                    {
                        selectedNumberBorder = numBorder;
                        if (numBorder.Child is TextBlock numText)
                            selectedNumberText = numText;
                    }
                    break;
                }
            }

            if (selectedRadioButton == null)
            {
                MessageBox.Show("Пожалуйста, выберите вариант ответа.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Отключаем все RadioButton
            foreach (StackPanel panel in AnswersPanel.Children.OfType<StackPanel>())
            {
                RadioButton rb = panel.Children.OfType<RadioButton>().FirstOrDefault();
                if (rb != null) rb.IsEnabled = false;
            }

            // Проверка ответа и подсветка (используем константы)
            bool isCorrect = (bool)selectedRadioButton.Tag;

            if (isCorrect)
            {
                correctAnswersCount++;
                selectedBorder.Background = CorrectBrush;
                selectedBorder.BorderBrush = SelectedBorderBrush;

                if (selectedNumberBorder != null)
                {
                    selectedNumberBorder.Background = SelectedBorderBrush;
                    if (selectedNumberText != null)
                        selectedNumberText.Foreground = Brushes.White;
                }
            }
            else
            {
                selectedBorder.Background = IncorrectBrush;
                selectedBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 68, 68));

                if (selectedNumberBorder != null)
                {
                    selectedNumberBorder.Background = new SolidColorBrush(Color.FromRgb(255, 68, 68));
                    if (selectedNumberText != null)
                        selectedNumberText.Foreground = Brushes.White;
                }

                // Подсвечиваем правильный ответ
                foreach (StackPanel panel in AnswersPanel.Children.OfType<StackPanel>())
                {
                    RadioButton rb = panel.Children.OfType<RadioButton>().FirstOrDefault();
                    Border container = panel.Children.OfType<Border>().FirstOrDefault();

                    if (rb != null && (bool)rb.Tag && container != selectedBorder)
                    {
                        container.Background = CorrectBrush;
                        container.BorderBrush = SelectedBorderBrush;

                        if (container.Child is Grid grid && grid.Children[0] is Border numBorder)
                        {
                            numBorder.Background = SelectedBorderBrush;
                            if (numBorder.Child is TextBlock numText)
                                numText.Foreground = Brushes.White;
                        }
                        break;
                    }
                }
            }

            // Переключаем в режим просмотра
            isFeedbackMode = true;
            NextQuestionButton.Content = "Продолжить →";
            NextQuestionButton.Background = SelectedBorderBrush;
        }

        private void DisplayCurrentQuestion()
        {
            if (currentQuestionIndex >= currentTestQuestions.Count)
            {
                ShowTestResults();
                return;
            }

            var question = currentTestQuestions[currentQuestionIndex];

            QuestionNumberText.Text = $"Вопрос {currentQuestionIndex + 1} из {currentTestQuestions.Count}";
            QuestionText.Text = question.Text;

            // Очищаем панель ответов
            AnswersPanel.Children.Clear();

            // Сбрасываем состояние кнопки
            NextQuestionButton.IsEnabled = false;
            NextQuestionButton.Content = "Ответить";
            NextQuestionButton.Background = SelectedBorderBrush;
            isFeedbackMode = false;

            // Создаем все варианты ответов
            int answerNumber = 1;
            foreach (var answer in question.Answers)
            {
                var answerPanel = CreateAnswerPanel(answer, answerNumber);
                AnswersPanel.Children.Add(answerPanel);
                answerNumber++;
            }
        }

        // Новый метод для создания панели ответа (вынесен для переиспользования)
        private StackPanel CreateAnswerPanel(Answer answer, int number)
        {
            // Контейнер для варианта ответа
            Border answerContainer = new Border
            {
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(0),
                BorderThickness = new Thickness(1),
                BorderBrush = DefaultBorderBrush,
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Cursor = Cursors.Hand,
                Tag = answer
            };

            // Эффект при наведении
            answerContainer.MouseEnter += (s, e) =>
            {
                answerContainer.Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));
                answerContainer.BorderBrush = SelectedBorderBrush;
            };

            answerContainer.MouseLeave += (s, e) =>
            {
                answerContainer.Background = Brushes.White;
                answerContainer.BorderBrush = DefaultBorderBrush;
            };

            // Внутренняя сетка
            Grid contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Круг с номером
            Border numberBorder = new Border
            {
                Width = 32,
                Height = 32,
                Background = LightGrayBrush,
                CornerRadius = new CornerRadius(16),
                Margin = new Thickness(12, 8, 12, 8),
                BorderThickness = new Thickness(1),
                BorderBrush = GrayBrush
            };

            TextBlock numberText = new TextBlock
            {
                Text = number.ToString(),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = DarkGrayBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            numberBorder.Child = numberText;

            // Текст ответа
            TextBlock answerText = new TextBlock
            {
                Text = answer.Text,
                FontSize = 15,
                Foreground = DarkBlueBrush,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 12, 8)
            };

            Grid.SetColumn(numberBorder, 0);
            Grid.SetColumn(answerText, 1);
            contentGrid.Children.Add(numberBorder);
            contentGrid.Children.Add(answerText);
            answerContainer.Child = contentGrid;

            // RadioButton (невидимый)
            RadioButton radioButton = new RadioButton
            {
                Tag = answer.IsCorrect,
                GroupName = "AnswerGroup",
                Opacity = 0,
                Width = 0,
                Height = 0,
                IsChecked = false
            };

            // Обработчик выбора
            radioButton.Checked += (s, e) =>
            {
                NextQuestionButton.IsEnabled = true;
                AnswerSelected(answerContainer, numberBorder, numberText);
            };

            // Клик по контейнеру
            answerContainer.MouseLeftButtonUp += (s, e) =>
            {
                radioButton.IsChecked = true;
            };

            // Собираем всё вместе
            StackPanel panel = new StackPanel();
            panel.Children.Add(radioButton);
            panel.Children.Add(answerContainer);

            return panel;
        }

        // Новый метод для обработки выбора ответа
        private void AnswerSelected(Border container, Border numberBorder, TextBlock numberText)
        {
            // Сбрасываем подсветку у всех
            foreach (StackPanel panel in AnswersPanel.Children.OfType<StackPanel>())
            {
                Border b = panel.Children.OfType<Border>().FirstOrDefault();
                if (b != null)
                {
                    b.BorderBrush = DefaultBorderBrush;
                    if (b.Child is Grid grid && grid.Children[0] is Border numBorder)
                    {
                        numBorder.Background = LightGrayBrush;
                        if (numBorder.Child is TextBlock numText)
                            numText.Foreground = DarkGrayBrush;
                    }
                }
            }

            // Подсвечиваем выбранный вариант
            container.BorderBrush = SelectedBorderBrush;
            numberBorder.Background = SelectedBorderBrush;
            numberText.Foreground = Brushes.White;
        }


        // #4 ИСПРАВЛЕНО: Удалён мёртвый метод SubmitAnswerButton_Click.
        // Он использовал устаревшую структуру DOM (Border > RadioButton),
        // несовместимую с CreateAnswerPanel (StackPanel > RadioButton + Border).
        // selectedRadioButton всегда был null → баг без видимого эффекта.

        private void ShowTestResults()
        {
            if (currentTestQuestions == null || !currentTestQuestions.Any())
            {
                return;
            }

            double percentage = (double)correctAnswersCount * 100 / currentTestQuestions.Count;
            string topicTitle = (TestSelectionListBox.SelectedItem as TestOption)?.Title ?? "Тест";

            int userId = UserManager.GetCurrentUserId();
            int? topicId = (TestSelectionListBox.SelectedItem as TestOption)?.TopicID;

            if (userId > 0)
            {
                try
                {
                    SqlDataAccess.SaveTestResult(
                        userId,
                        topicId,
                        currentTestQuestions.Count,
                        correctAnswersCount
                    );
                }
                catch (Exception) { }
            }

            // Обновляем текст в панели результатов
            FinalScoreText.Text = $"{percentage:F0}% — {correctAnswersCount} из {currentTestQuestions.Count}";


            WelcomePanel.Visibility = Visibility.Collapsed;
            TestSelectionPanel.Visibility = Visibility.Visible; // Левая панель видна
            QuestionDisplayPanel.Visibility = Visibility.Collapsed;
            TestResultPanel.Visibility = Visibility.Visible;    // Правая панель с результатами

            isTestInProgress = false;
            currentTestQuestions = null;
            currentQuestionIndex = 0;
            correctAnswersCount = 0;
            isFeedbackMode = false;

            CheckAndAwardAchievements();
        }


        private void LoadTopicsAndBuildTree()
        {
            // Если уже идет обновление - выходим
            if (_isUpdating) return;

            lock (_updateLock)
            {
                _isUpdating = true;
                try
                {
                    // Очищаем старые данные
                    allTopics = new List<Topic>();
                    linearizedTopics = new List<Topic>();

                    // Загружаем всё одним запросом через Dapper
                    using (var connection = new SqlConnection(SqlDataAccess.ConnectionString))
                    {
                        var sql = @"
                    SELECT * FROM Topics WHERE CourseID = @CourseId ORDER BY OrderIndex;
                    SELECT TopicID, COUNT(*) as Count FROM Questions GROUP BY TopicID;
                    SELECT COUNT(*) as TotalQuestions FROM Questions WHERE CourseID = @CourseId;
                ";

                        using (var multi = connection.QueryMultiple(sql, new { CourseId = SqlDataAccess.CurrentCourseId }))
                        {
                            // Читаем темы
                            allTopics = multi.Read<Topic>().ToList();

                            // Читаем количество вопросов по темам
                            var counts = multi.Read().ToList();
                            _questionCountCache.Clear();
                            foreach (var c in counts)
                            {
                                // Исправлено: явное приведение типов
                                int topicId = Convert.ToInt32(c.TopicID);
                                int count = Convert.ToInt32(c.Count);
                                _questionCountCache[topicId] = count;
                            }

                            // Читаем общее количество вопросов
                            var total = multi.ReadSingle<int>();
                            _questionCountCache[0] = total; // 0 - для общего теста
                        }
                    }

                    // Создаем линейный список для навигации
                    LinearizeTopics(allTopics);

                    // Перестраиваем дерево
                    RebuildTree(allTopics);

                    // Обновляем статус
                    if (StatusText != null)
                    {
                        StatusText.Text = $"Статус: Загружено {allTopics.Count} тем.";
                    }

                    ShowWelcomeScreen();
                    _lastCacheUpdate = DateTime.Now;
                }
                catch (System.Exception ex)
                {
                    // #20 ИСПРАВЛЕНО: Вместо технического MessageBox — дружелюбная панель ошибки
                    System.Diagnostics.Debug.WriteLine($"[DB ERROR] {ex.Message}");
                    ShowDbErrorPanel(ex.Message);

                    if (StatusText != null)
                        StatusText.Text = "Статус: нет подключения к базе данных.";
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }

        private void LinearizeTopics(List<Topic> topics)
        {
            linearizedTopics = new List<Topic>();

            // Берём только корневые темы (главы) в правильном порядке
            var roots = topics
                .Where(t => t.ParentTopicID == null)
                .OrderBy(t => t.OrderIndex)
                .ToList();

            foreach (var root in roots)
            {
                // Добавляем саму главу
                linearizedTopics.Add(root);

                // Рекурсивно добавляем подтемы
                AddChildrenLinear(topics, root.TopicID);
            }

            // Если вдруг есть темы без родителя которых нет среди корневых (старые данные)
            // добавляем их в конец чтобы не потерять
            var alreadyAdded = linearizedTopics.Select(t => t.TopicID).ToHashSet();
            foreach (var topic in topics.OrderBy(t => t.OrderIndex))
            {
                if (!alreadyAdded.Contains(topic.TopicID))
                    linearizedTopics.Add(topic);
            }
        }

        private void AddChildrenLinear(List<Topic> allTopics, int parentId)
        {
            var children = allTopics
                .Where(t => t.ParentTopicID == parentId)
                .OrderBy(t => t.OrderIndex)
                .ToList();

            foreach (var child in children)
            {
                linearizedTopics.Add(child);
                AddChildrenLinear(allTopics, child.TopicID); // рекурсия для вложенных
            }
        }

        private void AddChildItems(TreeViewItem parentItem, int parentId)
        {
            var children = allTopics.Where(t => t.ParentTopicID == parentId).OrderBy(t => t.OrderIndex);

            foreach (var childTopic in children)
            {
                TreeViewItem childItem = CreateTreeItem(childTopic);
                parentItem.Items.Add(childItem);
                AddChildItems(childItem, childTopic.TopicID);
            }
        }


        private TreeViewItem CreateTreeItem(Topic topic)
        {
            // Панель для кнопки и заголовка
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 2)
            };

            // ========== КНОПКА-ИКОНКА ==========
            var noteButton = new Button
            {
                Width = 22,
                Height = 22,
                Margin = new Thickness(0, 0, 4, 0),   // Отступ справа от иконки
                Tag = topic.TopicID,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                ToolTip = "Заметка к теме",
                Padding = new Thickness(0)
            };

            // Иконка (всегда синяя)
            var icon = new TextBlock
            {
                Text = "📝",
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 204))
            };

            noteButton.Content = icon;



            // ========== КЛИК ==========
            noteButton.Click += NoteButton_Click;

            // Сначала добавляем кнопку
            headerPanel.Children.Add(noteButton);

            // Потом название темы
            var titleBlock = new TextBlock
            {
                Text = topic.Title,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80))
            };
            headerPanel.Children.Add(titleBlock);

            return new TreeViewItem
            {
                Header = headerPanel,
                Tag = topic
            };
        }




        private void NoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!UserManager.IsLoggedIn)
            {
                MessageBox.Show("Чтобы писать заметки, нужно авторизоваться.", "Авторизация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Button btn = sender as Button;
            if (btn == null) return;

            int topicId = (int)btn.Tag;

            var noteWindow = new Computer_networks.Views.NoteWindow(topicId);
            noteWindow.Owner = this;
            noteWindow.ShowDialog();

            // Проверяем ачивки за заметки после закрытия окна
            if (UserManager.IsLoggedIn)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    CheckAndAwardAchievements();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        // ----------------------------------------------------------------------
        // МЕТОДЫ ОТОБРАЖЕНИЯ И НАВИГАЦИИ
        // ----------------------------------------------------------------------

        private void TableOfContentsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (TableOfContentsTree.SelectedItem is TreeViewItem selectedItem && selectedItem.Tag is Topic selectedTopic)
            {
                DisplayTopicContent(selectedTopic);
            }
        }


        private void DisplayTopicContent(Topic topic)
        {
            currentTopic = topic;
            currentTopicIndex = linearizedTopics.FindIndex(t => t.TopicID == topic.TopicID);
            UpdateNavigationButtons();

            var contentPanel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 20)
            };

            // Заголовок темы
            contentPanel.Children.Add(new TextBlock
            {
                Text = topic.Title,
                FontSize = Math.Min(currentFontSize + 10, 30),
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Margin = new Thickness(0, 0, 0, 15),
                TextWrapping = TextWrapping.Wrap
            });

            // Парсим HTML и рендерим в WPF-элементы
            string content = topic.Content ?? "";
            var elements = HtmlToWpf(content, currentFontSize);
            foreach (var el in elements)
                contentPanel.Children.Add(el);

            MainContentHost.Content = contentPanel;

            StatusText.Text = $"Статус: Отображается тема '{topic.Title}'.";
            LoadTopicResources(topic.TopicID);

            if (topic != null && UserManager.IsLoggedIn)
            {
                // Прогресс и ReviewLog только для студентов
                if (UserManager.CurrentUser.IsStudent)
                {
                    SqlDataAccess.LogTopicReview(UserManager.GetCurrentUserId(), topic.TopicID);
                    SqlDataAccess.UpdateUserProgress(UserManager.GetCurrentUserId(), topic.TopicID, 10);
                    CheckAndAwardAchievements();
                }
                PopulateTestSelectionListBox();
            }
        }
        // #20 ИСПРАВЛЕНО: Дружелюбный экран ошибки подключения к БД
        private void ShowDbErrorPanel(string technicalError)
        {
            if (MainContentHost == null) return;

            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(40)
            };

            panel.Children.Add(new TextBlock
            {
                Text = "⚠️",
                FontSize = 64,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            });

            panel.Children.Add(new TextBlock
            {
                Text = "Не удалось подключиться к базе данных",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(183, 28, 28)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });

            panel.Children.Add(new TextBlock
            {
                Text = "Проверьте, что SQL Server запущен, и повторите попытку.",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 24)
            });

            var retryBtn = new Button
            {
                Content = "🔄  Повторить подключение",
                Padding = new Thickness(24, 10, 24, 10),
                FontSize = 14,
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(21, 101, 192)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            retryBtn.Click += (_, __) => LoadTopicsAndBuildTree();
            panel.Children.Add(retryBtn);

            var detailsExpander = new Expander
            {
                Header = "Технические подробности",
                Margin = new Thickness(0, 20, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120))
            };
            detailsExpander.Content = new TextBlock
            {
                Text = technicalError,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140)),
                TextWrapping = TextWrapping.Wrap
            };
            panel.Children.Add(detailsExpander);

            MainContentHost.Content = panel;
        }

        private void ShowWelcomeScreen()
        {
            StackPanel welcomePanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            welcomePanel.Children.Add(new TextBlock
            {
                Text = "📚",
                FontSize = 72,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Margin = new Thickness(0, 0, 0, 20)
            });

            welcomePanel.Children.Add(new TextBlock
            {
                Text = "Добро пожаловать в электронный учебник!",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            });

            welcomePanel.Children.Add(new TextBlock
            {
                Text = "Выберите тему слева, чтобы начать изучение",
                FontSize = 18,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 15, 0, 20)
            });

            // Советы
            Border tipsBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(240, 247, 255)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 20, 0, 0)
            };

            StackPanel tipsPanel = new StackPanel();
            tipsPanel.Children.Add(new TextBlock
            {
                Text = "✨ Советы для начала:",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Margin = new Thickness(0, 0, 0, 10)
            });
            tipsPanel.Children.Add(new TextBlock
            {
                Text = "• Нажмите на любую тему в содержании слева",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 5, 0, 5)
            });
            tipsPanel.Children.Add(new TextBlock
            {
                Text = "• Используйте поиск для быстрого перехода",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 5, 0, 5)
            });
            tipsPanel.Children.Add(new TextBlock
            {
                Text = "• Добавляйте закладки на важные темы",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 5, 0, 5)
            });
            tipsPanel.Children.Add(new TextBlock
            {
                Text = "• Проходите тесты для проверки знаний",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 5, 0, 0)
            });

            tipsBorder.Child = tipsPanel;
            welcomePanel.Children.Add(tipsBorder);

            // Кнопка "Начать с первой темы"
            Button startButton = new Button
            {
                Margin = new Thickness(0, 30, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand,
                // Сбрасываем MaterialDesign стиль
                Style = new Style(typeof(Button))
            };

            // Контент — Border с текстом внутри (полностью обходим стили)
            startButton.Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(40, 18, 40, 18),
                Child = new TextBlock
                {
                    Text = "🚀 Начать с первой темы",
                    Foreground = Brushes.White,
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };

            startButton.Background = Brushes.Transparent;
            startButton.BorderThickness = new Thickness(0);
            startButton.Click += (s, e) => StartLearningButton_Click(s, e);


            welcomePanel.Children.Add(startButton);

            MainContentHost.Content = welcomePanel;
        }
        private void UpdateNavigationButtons()
        {

            if (PrevTopicButton == null || NextTopicButton == null) return;

            PrevTopicButton.IsEnabled = currentTopicIndex > 0;
            NextTopicButton.IsEnabled = currentTopicIndex < linearizedTopics.Count - 1;
        }

        private void NavigateTopic_Click(object sender, RoutedEventArgs e)
        {
            Button sourceButton = (Button)sender;
            int newIndex = currentTopicIndex;

            if (sourceButton == NextTopicButton)
                newIndex++;
            else if (sourceButton == PrevTopicButton)
                newIndex--;

            if (newIndex >= 0 && newIndex < linearizedTopics.Count)
            {
                Topic nextTopic = linearizedTopics[newIndex];
                DisplayTopicContent(nextTopic);
            }
        }
        private void StartLearningButton_Click(object sender, RoutedEventArgs e)
        {
            if (!linearizedTopics.Any())
            {
                MessageBox.Show("Темы не загружены. Проверьте подключение к базе данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Ищем первую корневую тему (без родителя)
            var firstTopic = linearizedTopics.FirstOrDefault(t => t.ParentTopicID == null)
                             ?? linearizedTopics.FirstOrDefault();

            if (firstTopic != null)
            {
                DisplayTopicContent(firstTopic);
            }
            else
            {
                MessageBox.Show("Не удалось найти тему для отображения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ----------------------------------------------------------------------
        // МЕТОДЫ ЗАКЛАДОК (Bookmarks)
        // ----------------------------------------------------------------------

        private void LoadBookmarks()
        {
            int userId = UserManager.GetCurrentUserId();

            if (userId <= 0)
            {
                BookmarksListView.ItemsSource = null;
                StatusText.Text = "Статус: Войдите в систему, чтобы увидеть закладки.";
                return;
            }

            try
            {
                List<Bookmark> bookmarks = SqlDataAccess.GetBookmarksByUserId(userId, SqlDataAccess.CurrentCourseId);
                BookmarksListView.ItemsSource = bookmarks;
                StatusText.Text = $"Статус: Загружено {bookmarks.Count} закладок.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке закладок: {ex.Message}", "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BookmarkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!UserManager.IsLoggedIn)
            {
                MessageBox.Show("Вы должны войти в систему, чтобы добавлять закладки.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (currentTopic == null)
            {
                MessageBox.Show("Сначала выберите тему, которую хотите добавить в закладки.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int userId = UserManager.GetCurrentUserId();

            try
            {
                SqlDataAccess.AddBookmark(userId, currentTopic.TopicID);
                LoadBookmarks();

                StatusText.Text = $"Статус: Тема '{currentTopic.Title}' добавлена в закладки.";
                MessageBox.Show($"Тема '{currentTopic.Title}' успешно добавлена в закладки!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                // 👇 ВЫЗОВ ПРОВЕРКИ
                CheckAndAwardAchievements();
            }
            catch (SqlException ex) when (ex.Number == 2627)
            {
                MessageBox.Show("Эта тема уже находится в ваших закладках.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении закладки: {ex.Message}", "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveBookmarkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!UserManager.IsLoggedIn)
            {
                MessageBox.Show("Вы должны войти в систему, чтобы удалять закладки.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Button source = (Button)sender;

            if (source.CommandParameter is Bookmark bookmarkToRemove)
            {
                MessageBoxResult result = MessageBox.Show(
                    $"Вы уверены, что хотите удалить закладку '{bookmarkToRemove.TopicTitle}'?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {

                    int userId = UserManager.GetCurrentUserId();

                    try
                    {
                        SqlDataAccess.RemoveBookmark(userId, bookmarkToRemove.TopicID);
                        LoadBookmarks(); // Обновляем список, чтобы удаленная закладка исчезла
                        StatusText.Text = $"Статус: Закладка '{bookmarkToRemove.TopicTitle}' удалена.";
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении закладки: {ex.Message}", "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BookmarksListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BookmarksListView.SelectedItem is Bookmark selectedBookmark)
            {
                Topic topicToDisplay = allTopics.FirstOrDefault(t => t.TopicID == selectedBookmark.TopicID);

                if (topicToDisplay != null)
                {
                    DisplayTopicContent(topicToDisplay);

                    if (MainTabControl.SelectedIndex != 0)
                    {
                        MainTabControl.SelectedIndex = 0;
                    }
                }
                BookmarksListView.SelectedItem = null;
            }
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is TabControl tabControl && e.Source == tabControl)
            {
                if (tabControl.SelectedItem == ProgressTabItem && !UserManager.IsLoggedIn)
                {
                    ShowAuthorizationRequiredDialog();
                    MainTabControl.SelectedIndex = 0;
                    return;
                }

                if (tabControl.SelectedItem == ProgressTabItem)
                {
                    LoadBookmarks();
                }

                if (tabControl.SelectedItem == TestingTabItem)
                {
                    if (!isTestInProgress)
                    {
                        // Показываем приветствие и список тестов
                        WelcomePanel.Visibility = Visibility.Visible;
                        TestSelectionPanel.Visibility = Visibility.Visible;
                        QuestionDisplayPanel.Visibility = Visibility.Collapsed;
                        TestResultPanel.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        // Если тест в процессе - показываем вопросы
                        WelcomePanel.Visibility = Visibility.Collapsed;
                        TestSelectionPanel.Visibility = Visibility.Collapsed;
                        QuestionDisplayPanel.Visibility = Visibility.Visible;
                        TestResultPanel.Visibility = Visibility.Collapsed;
                    }
                }

                if (tabControl.SelectedItem == LabsTabItem && !UserManager.IsLoggedIn)
                {
                    ShowAuthorizationRequiredDialog();
                    MainTabControl.SelectedIndex = 0;
                    return;
                }

                if (tabControl.SelectedItem == LabsTabItem)
                {
                    LoadLabsData();
                }

                if (tabControl.SelectedIndex == 0)
                {
                    // Учебник
                }
            }
        }

        private void ShowAuthorizationRequiredDialog()
        {
            var result = MessageBox.Show(
                "Для просмотра прогресса и статистики необходимо авторизоваться.\n\nХотите войти в систему?",
                "Требуется авторизация",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ShowLoginWindow();
            }
        }

        private void ShowLoginWindow()
        {
            RegistrationWindow registrationWindow = new RegistrationWindow();

            if (registrationWindow.ShowDialog() == true)
            {
                // После успешного входа обновляем интерфейс
                UpdateUserInterface();
                LoadUserData(UserManager.GetCurrentUserId());
            }
        }
        private void IncreaseFontSizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentSizeIndex < fontSizes.Length - 1)
            {
                currentSizeIndex++;
            }

            currentFontSize = fontSizes[currentSizeIndex];
            ApplyFontSizeToContent(currentFontSize);
            UpdateFontSizeButtons();
            StatusText.Text = $"Статус: Размер шрифта увеличен до {currentFontSize}pt.";
        }

        private void DecreaseFontSizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentSizeIndex > 0)
            {
                currentSizeIndex--;
            }

            currentFontSize = fontSizes[currentSizeIndex];
            ApplyFontSizeToContent(currentFontSize);
            UpdateFontSizeButtons();
            StatusText.Text = $"Статус: Размер шрифта уменьшен до {currentFontSize}pt.";
        }

        private void UpdateFontSizeButtons()
        {
            IncreaseFontSizeButton.IsEnabled = (currentSizeIndex < fontSizes.Length - 1);
            DecreaseFontSizeButton.IsEnabled = (currentSizeIndex > 0);
        }

        private static void SetBrowserEmulationMode()
        {
            try
            {
                string appName = System.IO.Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);

                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION", true)
                    ?? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"SOFTWARE\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"))
                {
                    // 11001 = IE11 режим
                    key.SetValue(appName, 11001, Microsoft.Win32.RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BrowserEmulation] Ошибка: {ex.Message}");
            }
        }

        private void ApplyFontSizeToContent(double newSize)
        {
            if (currentTopic != null)
            {
                DisplayTopicContent(currentTopic);
            }
        }
        // Рекурсивная функция для установки размера шрифта для всех TextBlock
        private void SetFontSizeRecursively(DependencyObject parent, double newSize)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is TextBlock textBlock)
                {
                    if (textBlock.FontSize < 20.1)
                    {
                        textBlock.FontSize = newSize;
                    }
                }
                else if (child is ContentControl contentControl && contentControl.Content is FrameworkElement innerContent)
                {
                    SetFontSizeRecursively(innerContent, newSize);
                }

                SetFontSizeRecursively(child, newSize);
            }
        }

        // ----------------------------------------------------------------------
        // МЕТОДЫ ПОИСКА (SearchBox)
        // ----------------------------------------------------------------------
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchBox == null) return;

            // Получаем текст для поиска
            string searchText = SearchBox.Text.Trim();

            // Если текст не изменился - выходим
            if (searchText == _lastSearchText) return;
            _lastSearchText = searchText;

            // Используем таймер для задержки поиска (чтобы не искать при каждом нажатии)
            if (_searchTimer != null)
            {
                _searchTimer.Stop();
            }
            else
            {
                _searchTimer = new DispatcherTimer();
                _searchTimer.Tick += SearchTimer_Tick;
            }

            _searchTimer.Interval = TimeSpan.FromMilliseconds(300);
            _searchTimer.Tag = searchText; // Сохраняем текст поиска
            _searchTimer.Start();
        }

        private void SearchTimer_Tick(object sender, EventArgs e)
        {
            var timer = sender as DispatcherTimer;
            timer.Stop();

            string searchText = timer.Tag as string ?? "";
            PerformSearch(searchText);
        }

        private void PerformSearch(string searchText)
        {
            // Логика обработки СБРОСА (Пустой поиск или текст-подсказка)
            if (string.IsNullOrWhiteSpace(searchText) || searchText == "Поиск по темам...")
            {
                if (TableOfContentsTree != null && _lastFilteredTopics != allTopics)
                {
                    RebuildTree(allTopics);
                    _lastFilteredTopics = allTopics;
                }

                if (_allTestOptions != null && TestSelectionListBox != null)
                {
                    TestSelectionListBox.ItemsSource = _allTestOptions;
                    if (TestSelectionListBox.Items.Count > 0)
                    {
                        TestSelectionListBox.SelectedIndex = 0;
                    }
                }

                if (StatusText != null)
                {
                    StatusText.Text = "Статус: Поиск сброшен. Отображаются все темы.";
                }
                return;
            }

            // Поиск с использованием кэша
            string searchLower = searchText.ToLowerInvariant();

            // Ищем в кэше или выполняем поиск
            if (!_searchCache.TryGetValue(searchLower, out var filteredTopics))
            {
                filteredTopics = allTopics
                    .Where(t => t.Title.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                _searchCache[searchLower] = filteredTopics;
            }

            // Обновляем дерево только если результаты изменились
            if (_lastFilteredTopics != filteredTopics)
            {
                RebuildTree(filteredTopics);
                _lastFilteredTopics = filteredTopics;
            }

            // Фильтруем тесты
            if (_allTestOptions != null && TestSelectionListBox != null)
            {
                var filteredTests = _allTestOptions
                    .Where(t => t.Title.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                TestSelectionListBox.ItemsSource = filteredTests;
                if (filteredTests.Any())
                {
                    TestSelectionListBox.SelectedIndex = 0;
                }
            }

            if (StatusText != null)
            {
                StatusText.Text = $"Статус: Отображаются результаты поиска по запросу '{searchText}'.";
            }
        }
        private void RebuildTree(List<Topic> topics)
        {

            if (TableOfContentsTree == null) return;

            TableOfContentsTree.Items.Clear();

            var rootTopics = topics.Where(t => t.ParentTopicID == null).OrderBy(t => t.OrderIndex);

            foreach (var topic in rootTopics)
            {
                TreeViewItem rootItem = CreateTreeItem(topic);
                AddChildItems(rootItem, topic.TopicID);
                TableOfContentsTree.Items.Add(rootItem);
            }
        }

        private void AddFilteredChildItems(TreeViewItem parentItem, int parentId, HashSet<int> topicsToDisplay)
        {
            var children = allTopics
                .Where(t => t.ParentTopicID == parentId && topicsToDisplay.Contains(t.TopicID))
                .OrderBy(t => t.OrderIndex);

            foreach (var childTopic in children)
            {
                TreeViewItem childItem = CreateTreeItem(childTopic);
                parentItem.Items.Add(childItem);

                AddFilteredChildItems(childItem, childTopic.TopicID, topicsToDisplay);

                parentItem.IsExpanded = true;
            }
        }
        // #13 ИСПРАВЛЕНО: RemoveTextOnFocus и AddTextOnLostFocus удалены.
        // Плейсхолдер теперь работает через PlaceholderTextBehavior attached property в XAML.

        private void ShowGlossary_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Считаем открытие глоссария для ачивок
                if (UserManager.IsLoggedIn)
                {
                    int uid = UserManager.GetCurrentUserId();
                    try
                    {
                        SqlDataAccess.IncrementGlossaryOpenCount(uid);
                        System.Diagnostics.Debug.WriteLine($"[ShowGlossary] IncrementGlossaryOpenCount uid={uid} вызван");
                    }
                    catch (Exception exInc)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ShowGlossary] IncrementGlossaryOpenCount ОШИБКА: {exInc.Message}");
                    }
                }

                var glossaryWindow = new Views.GlossaryWindow();
                glossaryWindow.Owner = this;
                glossaryWindow.ShowDialog();

                // Проверяем ачивки за глоссарий после закрытия
                if (UserManager.IsLoggedIn)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CheckAndAwardAchievements();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии словаря: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // Метод, вызываемый кнопкой "Справка (F1)"
        private void ShowHelp_Click(object sender, RoutedEventArgs e)
        {
            OpenHelpWindow();
        }

        // Новый обработчик для горячей клавиши F1 (добавленный к Grid в XAML)
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F1)
            {
                OpenHelpWindow();
                e.Handled = true;
            }
            else if (e.Key == Key.F2)
            {
                ShowGlossary_Click(sender, e); // 👈 Вызываем тот же метод, что и по кнопке
                e.Handled = true;
            }
        }

        private void OpenHelpWindow()
        {
            try
            {
                var helpWindow = new HelpWindow();
                helpWindow.Owner = this;
                helpWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии справки: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StatisticsTabItem_GotFocus(object sender, RoutedEventArgs e)
        {
            int userId = UserManager.GetCurrentUserId();

            // Если пользователь не вошел, LoadProgressData() сам очистит список
            LoadProgressData();
        }
        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            if (UserManager.IsLoggedIn)
            {
                var user = UserManager.CurrentUser;
                // Для Администратора и Преподавателя — открываем окно профиля
                if (user.IsAdmin || user.IsTeacher)
                {
                    var profileWindow = new Views.ProfileWindow { Owner = this };
                    profileWindow.ShowDialog();
                    // Обновляем только кнопку тулбара — без баннера приветствия
                    UpdateToolbarOnly();
                }
                else
                {
                    // Студент — просто подтверждение выхода (у него нет отдельного профиля)
                    MessageBoxResult result = MessageBox.Show(
                        "Вы вошли как студент. Хотите выйти из системы?",
                        "Выход",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        UserManager.Logout();
                        UpdateUserInterface();
                    }
                }
                return;
            }

            RegistrationWindow registrationWindow = new RegistrationWindow();
            if (registrationWindow.ShowDialog() == true)
            {
                UpdateUserInterface();
                LoadUserData(UserManager.GetCurrentUserId());
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Вы точно хотите выйти из системы?",
                "Подтверждение выхода",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                UserManager.Logout();
                UpdateUserInterface();
                MessageBox.Show("Вы успешно вышли из системы.", "Выход",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void UpdateUserInterface()
        {
            if (RegisterButton == null) return;
            if (AdminSettingsTabItem == null) return;

            if (UserManager.IsLoggedIn)
            {
                var user = UserManager.CurrentUser;

                System.Diagnostics.Debug.WriteLine($"[UpdateUI] User={user.Username}, RoleID={user.RoleID}");

                // Загружаем аватар из БД (если не сохранён — берём дефолт по роли)
                string avatar = SqlDataAccess.GetUserAvatar(user.UserID);
                if (string.IsNullOrEmpty(avatar))
                {
                    if (user.RoleID == 1) avatar = "👑";
                    else if (user.RoleID == 2) avatar = "📚";
                    else avatar = "🎓";
                }

                if (user.RoleID == 1)  // Администратор
                {
                    RegisterButton.Content = $"{avatar} {user.Username} (Администратор)";
                    ShowWelcomeBanner(user.Username, "Администратор",
                        "Добро пожаловать! Вам доступно полное управление системой.",
                        avatar, "#8B0000", "#CC3333");
                }
                else if (user.RoleID == 2)  // Преподаватель
                {
                    RegisterButton.Content = $"{avatar} {user.Username} (Преподаватель)";
                    ShowWelcomeBanner(user.Username, "Преподаватель",
                        "Добро пожаловать! Вы можете управлять курсами и тестами.",
                        avatar, "#003D66", "#007ACC");
                }
                else if (user.RoleID == 3)  // Студент
                {
                    RegisterButton.Content = $"{avatar} {user.Username} (Студент)";
                    ShowWelcomeBanner(user.Username, "Студент",
                        "Добро пожаловать! Изучайте материалы и проходите тесты.",
                        avatar, "#1A6B33", "#33AA33");
                }
                else
                {
                    RegisterButton.Content = $"👤 {user.Username}";
                }

                // Показываем вкладку администрирования для Админа и Преподавателя
                bool canAccessAdmin = (user.RoleID == 1 || user.RoleID == 2);
                AdminSettingsTabItem.Visibility = canAccessAdmin ? Visibility.Visible : Visibility.Collapsed;

                // Кнопка выхода — всегда видна при входе
                if (LogoutButton != null)
                    LogoutButton.Visibility = Visibility.Visible;

                // 👇 ВАЖНО: Если пользователь не имеет доступа к админке - очищаем содержимое
                if (!canAccessAdmin)
                {
                    AdminContent.Content = null;
                }

                System.Diagnostics.Debug.WriteLine($"[UpdateUI] Button text set to: {RegisterButton.Content}");
            }
            else
            {
                RegisterButton.Content = "👤 Регистрация/Вход";
                AdminSettingsTabItem.Visibility = Visibility.Collapsed;
                if (LogoutButton != null) LogoutButton.Visibility = Visibility.Collapsed;
                if (WelcomeBanner != null) WelcomeBanner.Visibility = Visibility.Collapsed;

                // Очищаем содержимое вкладки администрирования —
                // при следующем входе AdminSettingsTabItem_GotFocus создаст новую панель
                AdminContent.Content = null;

                // Переключаемся на вкладку "Учебник"
                MainTabControl.SelectedIndex = 0;

                System.Diagnostics.Debug.WriteLine($"[UpdateUI] Logged out state");
            }

            LoadUserData(UserManager.GetCurrentUserId());
            LoadLabsData();
        }

        /// <summary>
        /// Обновляет только текст кнопки тулбара (аватар + имя) — без баннера приветствия.
        /// Вызывается после закрытия окна профиля.
        /// </summary>
        private void UpdateToolbarOnly()
        {
            if (RegisterButton == null || !UserManager.IsLoggedIn) return;
            var user = UserManager.CurrentUser;

            string avatar = SqlDataAccess.GetUserAvatar(user.UserID);
            if (string.IsNullOrEmpty(avatar))
            {
                if (user.RoleID == 1) avatar = "👑";
                else if (user.RoleID == 2) avatar = "📚";
                else avatar = "🎓";
            }

            if (user.RoleID == 1)
                RegisterButton.Content = $"{avatar} {user.Username} (Администратор)";
            else if (user.RoleID == 2)
                RegisterButton.Content = $"{avatar} {user.Username} (Преподаватель)";
            else if (user.RoleID == 3)
                RegisterButton.Content = $"{avatar} {user.Username} (Студент)";
            else
                RegisterButton.Content = $"👤 {user.Username}";

            System.Diagnostics.Debug.WriteLine($"[UpdateToolbarOnly] {RegisterButton.Content}");
        }
        // ── Плашка-приветствие ────────────────────────────────────────
        private DispatcherTimer _bannerTimer;

        private void ShowWelcomeBanner(string username, string roleName, string subtitle,
                                       string icon, string darkColor, string lightColor)
        {
            if (WelcomeBanner == null) return;

            // Цвет фона — градиент через кисть
            var gradientBrush = new LinearGradientBrush();
            gradientBrush.StartPoint = new System.Windows.Point(0, 0);
            gradientBrush.EndPoint = new System.Windows.Point(1, 0);
            gradientBrush.GradientStops.Add(new GradientStop(
                (Color)ColorConverter.ConvertFromString(darkColor), 0.0));
            gradientBrush.GradientStops.Add(new GradientStop(
                (Color)ColorConverter.ConvertFromString(lightColor), 1.0));
            WelcomeBanner.Background = gradientBrush;

            WelcomeBannerIcon.Text = icon;
            WelcomeBannerTitle.Text = $"Добро пожаловать, {username}! ({roleName})";
            WelcomeBannerSubtitle.Text = subtitle;
            WelcomeBanner.Visibility = Visibility.Visible;

            // Автоматически скрываем через 5 секунд
            _bannerTimer?.Stop();
            _bannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _bannerTimer.Tick += (s, e) =>
            {
                _bannerTimer.Stop();
                if (WelcomeBanner != null) WelcomeBanner.Visibility = Visibility.Collapsed;
            };
            _bannerTimer.Start();
        }

        private void WelcomeBannerClose_Click(object sender, RoutedEventArgs e)
        {
            _bannerTimer?.Stop();
            WelcomeBanner.Visibility = Visibility.Collapsed;
        }

        // ── Обработчики ролевых кнопок ───────────────────────────────
        private void LoadUserData(int userId)
        {
            if (userId <= 0)
            {
                if (BookmarksListView != null) BookmarksListView.ItemsSource = null;
                if (StatisticsListBox != null) StatisticsListBox.ItemsSource = null;
                return;
            }

            LoadBookmarks();
            LoadProgressData();
        }
        private void LoadProgressData()
        {
            int userId = UserManager.GetCurrentUserId();

            // Используем StatisticsListBox
            if (userId > 0 && StatisticsListBox != null)
            {
                try
                {
                    // 👇 ПЕРЕДАЁМ ТЕКУЩИЙ КУРС
                    List<TestStatistic> statistics = SqlDataAccess.GetTestStatisticsByUserId(userId, SqlDataAccess.CurrentCourseId);

                    // Привязываем данные к StatisticsListBox
                    StatisticsListBox.ItemsSource = statistics;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке статистики: {ex.Message}", "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
                    if (StatisticsListBox != null)
                    {
                        StatisticsListBox.ItemsSource = null;
                    }
                }
            }
            else
            {
                // Если не авторизован, очищаем StatisticsListBox
                if (StatisticsListBox != null)
                {
                    StatisticsListBox.ItemsSource = null;
                }
            }
        }

        private void LoadLabsData()
        {
            try
            {
                SqlDataAccess.EnsureDbSchema();
                SqlDataAccess.EnsureTeacherCoursesSchema();

                bool canManage = UserManager.IsLoggedIn
                    && (UserManager.CurrentUser.IsAdmin || UserManager.CurrentUser.IsTeacher);

                // Определяем какой курс показывать в списке лаб
                int courseToShow = _selectedLabCourseId > 0
                    ? _selectedLabCourseId
                    : SqlDataAccess.CurrentCourseId;

                _currentCourseLabs = SqlDataAccess.GetLabWorksByCourse(courseToShow, canManage);

                if (LabsListBox != null)
                    LabsListBox.ItemsSource = _currentCourseLabs;

                if (MyReportsListBox != null)
                {
                    if (UserManager.IsLoggedIn)
                    {
                        _currentUserLabReports = SqlDataAccess.GetLabReportSubmissionsByUser(
                            UserManager.GetCurrentUserId(), SqlDataAccess.CurrentCourseId);
                        MyReportsListBox.ItemsSource = _currentUserLabReports;
                    }
                    else
                    {
                        _currentUserLabReports = new List<LabReportSubmission>();
                        MyReportsListBox.ItemsSource = null;
                    }
                }

                // Загружаем курсы преподавателя для комбобокса выбора курса
                if (canManage)
                {
                    _teacherCourses = SqlDataAccess.GetCoursesForTeacher(UserManager.GetCurrentUserId());

                    var labCourseSelector = this.FindName("LabCourseSelector") as ComboBox;
                    if (labCourseSelector != null)
                    {
                        labCourseSelector.ItemsSource = _teacherCourses;
                        if (labCourseSelector.SelectedItem == null)
                        {
                            var current = _teacherCourses.FirstOrDefault(c => c.CourseID == SqlDataAccess.CurrentCourseId)
                                          ?? _teacherCourses.FirstOrDefault();
                            labCourseSelector.SelectedItem = current;
                            _selectedLabCourseId = current?.CourseID ?? SqlDataAccess.CurrentCourseId;
                        }
                    }
                    else
                    {
                        // Комбобокса нет в XAML — используем текущий курс
                        _selectedLabCourseId = SqlDataAccess.CurrentCourseId;
                    }
                }

                if (SelectedLabStatusText != null)
                    SelectedLabStatusText.Text = _currentCourseLabs.Any()
                        ? $"Загружено лабораторных: {_currentCourseLabs.Count}"
                        : "Для текущего курса пока нет лабораторных работ.";

                UpdateSelectedLabPanel();
                UpdateLabControlsByRole();
            }
            catch (Exception ex)
            {
                if (SelectedLabStatusText != null)
                    SelectedLabStatusText.Text = $"Ошибка загрузки лабораторных: {ex.Message}";
            }
        }

        private void LabCourseSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!((sender as ComboBox)?.SelectedItem is Course selected)) return;
            _selectedLabCourseId = selected.CourseID;

            bool canManage = UserManager.IsLoggedIn
                && (UserManager.CurrentUser.IsAdmin || UserManager.CurrentUser.IsTeacher);
            _currentCourseLabs = SqlDataAccess.GetLabWorksByCourse(selected.CourseID, canManage);

            if (LabsListBox != null) LabsListBox.ItemsSource = _currentCourseLabs;

            if (SelectedLabStatusText != null)
                SelectedLabStatusText.Text = _currentCourseLabs.Any()
                    ? $"Курс «{selected.CourseName}»: {_currentCourseLabs.Count} лабораторных"
                    : $"Курс «{selected.CourseName}»: лабораторных пока нет.";

            UpdateLabControlsByRole();
        }

        // Вспомогательный метод: ищет кнопку по имени (безопасно для старых версий XAML)
        private Button FindLabButton(string name) => this.FindName(name) as Button;

        private void UpdateLabControlsByRole()
        {
            bool canCreateLab = UserManager.IsLoggedIn && (UserManager.CurrentUser.IsAdmin || UserManager.CurrentUser.IsTeacher);
            bool canUploadReport = UserManager.IsLoggedIn && UserManager.CurrentUser.IsStudent;
            bool hasSelectedLab = LabsListBox?.SelectedItem is LabWork;

            if (LabTitleTextBox != null) LabTitleTextBox.IsEnabled = canCreateLab;
            if (LabDescriptionTextBox != null) LabDescriptionTextBox.IsEnabled = canCreateLab;
            if (LabDeadlineDatePicker != null) LabDeadlineDatePicker.IsEnabled = canCreateLab;
            if (AddLabButton != null) AddLabButton.IsEnabled = canCreateLab;

            // Используем FindName — безопасно работает даже если кнопки нет в XAML
            var editBtn = FindLabButton("EditLabButton");
            if (editBtn != null) editBtn.IsEnabled = canCreateLab && hasSelectedLab;

            var deleteBtn = FindLabButton("DeleteLabButton");
            if (deleteBtn != null) deleteBtn.IsEnabled = canCreateLab && hasSelectedLab;

            if (UploadLabAssignmentFileButton != null) UploadLabAssignmentFileButton.IsEnabled = canCreateLab && hasSelectedLab;
            if (UploadLabReportButton != null)
                UploadLabReportButton.IsEnabled = canUploadReport && hasSelectedLab;

            if (LabAdminPanel != null) LabAdminPanel.Visibility = canCreateLab ? Visibility.Visible : Visibility.Collapsed;
            if (TeacherReportsPanel != null) TeacherReportsPanel.Visibility = canCreateLab ? Visibility.Visible : Visibility.Collapsed;
            if (OpenLabAssignmentButton != null) OpenLabAssignmentButton.IsEnabled = hasSelectedLab;
        }

        private void LabsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSelectedLabPanel();
            UpdateLabControlsByRole();

            // Заполняем поля формы данными выбранной лабы (для редактирования)
            bool canEdit = UserManager.IsLoggedIn && (UserManager.CurrentUser.IsAdmin || UserManager.CurrentUser.IsTeacher);
            if (canEdit && LabsListBox?.SelectedItem is LabWork selectedLab)
            {
                if (LabTitleTextBox != null) LabTitleTextBox.Text = selectedLab.Title;
                if (LabDescriptionTextBox != null) LabDescriptionTextBox.Text = selectedLab.Description ?? string.Empty;
                if (LabDeadlineDatePicker != null) LabDeadlineDatePicker.SelectedDate = selectedLab.Deadline;
            }
        }

        private void UpdateSelectedLabPanel()
        {
            if (!(LabsListBox?.SelectedItem is LabWork selectedLab))
            {
                if (SelectedLabTitleText != null) SelectedLabTitleText.Text = "Лабораторная не выбрана";
                if (SelectedLabMetaText != null) SelectedLabMetaText.Text = "Выберите лабораторную слева, чтобы увидеть детали и действия.";
                if (LabSubmissionsListBox != null) LabSubmissionsListBox.ItemsSource = null;
                if (SelectedLabStatusText != null) SelectedLabStatusText.Text = _currentCourseLabs.Any()
                        ? $"Загружено лабораторных: {_currentCourseLabs.Count}"
                        : "Для текущего курса пока нет лабораторных работ.";
                return;
            }

            var deadlineText = selectedLab.Deadline.HasValue ? selectedLab.Deadline.Value.ToString("dd.MM.yyyy") : "не указан";
            string assignmentText = string.IsNullOrWhiteSpace(selectedLab.FileName) ? "файл задания не загружен" : $"файл задания: {selectedLab.FileName}";

            if (SelectedLabTitleText != null) SelectedLabTitleText.Text = selectedLab.Title;
            if (SelectedLabMetaText != null) SelectedLabMetaText.Text = $"Срок: {deadlineText}. {assignmentText}";

            if (SelectedLabStatusText != null)
            {
                bool userSubmitted = UserManager.IsLoggedIn && _currentUserLabReports.Any(r => r.LabWorkID == selectedLab.LabWorkID);
                SelectedLabStatusText.Text = userSubmitted
                    ? $"Выбрана: {selectedLab.Title}. Статус: отчет сдан."
                    : $"Выбрана: {selectedLab.Title}. Статус: отчет не сдан.";
            }

            if (UserManager.IsLoggedIn && (UserManager.CurrentUser.IsAdmin || UserManager.CurrentUser.IsTeacher))
                LabSubmissionsListBox.ItemsSource = SqlDataAccess.GetLabReportSubmissionsByLab(selectedLab.LabWorkID);
            else
                LabSubmissionsListBox.ItemsSource = null;
        }

        private void AddLabButton_Click(object sender, RoutedEventArgs e)
        {
            if (!UserManager.IsLoggedIn || (!UserManager.CurrentUser.IsAdmin && !UserManager.CurrentUser.IsTeacher))
            {
                MessageBox.Show("Добавлять лабораторные могут только преподаватель или администратор.");
                return;
            }

            string title = (LabTitleTextBox?.Text ?? string.Empty).Trim();
            string description = (LabDescriptionTextBox?.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Введите название лабораторной работы.");
                return;
            }

            // Определяем целевой курс — из комбобокса или текущий
            int targetCourseId = _selectedLabCourseId > 0
                ? _selectedLabCourseId
                : SqlDataAccess.CurrentCourseId;

            // Проверяем права на этот курс
            if (!SqlDataAccess.CanManageLabsInCourse(UserManager.GetCurrentUserId(), targetCourseId))
            {
                MessageBox.Show(
                    "У вас нет прав на добавление лабораторных в этот курс.\nОбратитесь к администратору.",
                    "Нет доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SqlDataAccess.EnsureDbSchema();
                SqlDataAccess.AddLabWork(
                    targetCourseId,
                    title,
                    description,
                    LabDeadlineDatePicker?.SelectedDate,
                    UserManager.GetCurrentUserId());

                LabTitleTextBox.Text = string.Empty;
                LabDescriptionTextBox.Text = string.Empty;
                LabDeadlineDatePicker.SelectedDate = null;

                LoadLabsData();

                var courseName = _teacherCourses.FirstOrDefault(c => c.CourseID == targetCourseId)?.CourseName
                                 ?? targetCourseId.ToString();
                StatusText.Text = $"Статус: Лабораторная добавлена в курс «{courseName}».";
                MessageBox.Show($"Лабораторная «{title}» добавлена в курс «{courseName}».",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления лабораторной: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditLabButton_Click(object sender, RoutedEventArgs e)
        {
            if (!UserManager.IsLoggedIn || (!UserManager.CurrentUser.IsAdmin && !UserManager.CurrentUser.IsTeacher))
            {
                MessageBox.Show("Редактировать лабораторные могут только преподаватель или администратор.");
                return;
            }

            if (!(LabsListBox?.SelectedItem is LabWork selectedLab))
            {
                MessageBox.Show("Сначала выберите лабораторную работу для редактирования.");
                return;
            }

            // Проверка прав: преподаватель может редактировать только лабы своих курсов
            if (!SqlDataAccess.CanManageLabsInCourse(UserManager.GetCurrentUserId(), selectedLab.CourseID))
            {
                MessageBox.Show(
                    "Вы не можете редактировать лабораторные другого преподавателя.",
                    "Нет доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string title = (LabTitleTextBox?.Text ?? string.Empty).Trim();
            string description = (LabDescriptionTextBox?.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Название лабораторной работы не может быть пустым.");
                return;
            }

            try
            {
                SqlDataAccess.UpdateLabWork(
                    selectedLab.LabWorkID,
                    title,
                    description,
                    LabDeadlineDatePicker?.SelectedDate);

                LoadLabsData();
                StatusText.Text = $"Статус: Лабораторная \"{title}\" успешно обновлена.";
                MessageBox.Show("Лабораторная работа успешно обновлена.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении лабораторной: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteLabButton_Click(object sender, RoutedEventArgs e)
        {
            if (!UserManager.IsLoggedIn || (!UserManager.CurrentUser.IsAdmin && !UserManager.CurrentUser.IsTeacher))
            {
                MessageBox.Show("Удалять лабораторные могут только преподаватель или администратор.");
                return;
            }

            if (!(LabsListBox?.SelectedItem is LabWork selectedLab))
            {
                MessageBox.Show("Сначала выберите лабораторную работу для удаления.");
                return;
            }

            // Проверка прав: преподаватель может удалять только лабы своих курсов
            if (!SqlDataAccess.CanManageLabsInCourse(UserManager.GetCurrentUserId(), selectedLab.CourseID))
            {
                MessageBox.Show(
                    "Вы не можете удалять лабораторные другого преподавателя.",
                    "Нет доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Вы уверены, что хотите удалить лабораторную \"{selectedLab.Title}\"?\n\nВсе загруженные отчёты студентов также будут удалены. Это действие необратимо.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                SqlDataAccess.DeleteLabWork(selectedLab.LabWorkID);

                if (LabTitleTextBox != null) LabTitleTextBox.Text = string.Empty;
                if (LabDescriptionTextBox != null) LabDescriptionTextBox.Text = string.Empty;
                if (LabDeadlineDatePicker != null) LabDeadlineDatePicker.SelectedDate = null;

                LoadLabsData();
                StatusText.Text = $"Статус: Лабораторная \"{selectedLab.Title}\" удалена.";
                MessageBox.Show("Лабораторная работа удалена.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении лабораторной: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UploadLabAssignmentFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!UserManager.IsLoggedIn || (!UserManager.CurrentUser.IsAdmin && !UserManager.CurrentUser.IsTeacher))
            {
                MessageBox.Show("Загружать файл лабораторной могут только преподаватель или администратор.");
                return;
            }

            if (!(LabsListBox?.SelectedItem is LabWork selectedLab))
            {
                MessageBox.Show("Сначала выберите лабораторную работу.");
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Выберите файл лабораторной работы",
                Filter = "Документы и архивы|*.pdf;*.doc;*.docx;*.ppt;*.pptx;*.zip;*.rar;*.txt|Все файлы|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            var fileInfo = new FileInfo(dialog.FileName);
            string labsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Uploads", "LabAssignments", SqlDataAccess.CurrentCourseId.ToString());
            Directory.CreateDirectory(labsDir);

            string storedName = $"{selectedLab.LabWorkID}_{DateTime.Now:yyyyMMddHHmmss}_{Path.GetFileName(dialog.FileName)}";
            string destinationPath = Path.Combine(labsDir, storedName);
            File.Copy(dialog.FileName, destinationPath, true);

            SqlDataAccess.SaveLabAssignmentFile(
                selectedLab.LabWorkID,
                fileInfo.Name,
                storedName,
                destinationPath,
                (int)Math.Ceiling(fileInfo.Length / 1024.0));

            LoadLabsData();
            StatusText.Text = $"Статус: Файл лабораторной \"{selectedLab.Title}\" загружен.";
            MessageBox.Show("Файл лабораторной успешно загружен.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenLabAssignmentButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(LabsListBox?.SelectedItem is LabWork selectedLab))
            {
                MessageBox.Show("Сначала выберите лабораторную работу.");
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedLab.FilePath) || !File.Exists(selectedLab.FilePath))
            {
                MessageBox.Show("Для этой лабораторной еще не загружен файл задания.");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = selectedLab.FilePath,
                UseShellExecute = true
            });
        }

        private void UploadLabReportButton_Click(object sender, RoutedEventArgs e)
        {
            if (!UserManager.IsLoggedIn || !UserManager.CurrentUser.IsStudent)
            {
                MessageBox.Show("Загрузка отчетов доступна только студентам.");
                return;
            }

            if (!(LabsListBox?.SelectedItem is LabWork selectedLab))
            {
                MessageBox.Show("Сначала выберите лабораторную работу.");
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Выберите файл отчета",
                Filter = "Документы|*.pdf;*.doc;*.docx;*.rtf;*.txt;*.zip|Все файлы|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            var fileInfo = new FileInfo(dialog.FileName);
            string reportsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Uploads", "LabReports", SqlDataAccess.CurrentCourseId.ToString());
            Directory.CreateDirectory(reportsDir);

            string storedName = $"{selectedLab.LabWorkID}_{UserManager.GetCurrentUserId()}_{DateTime.Now:yyyyMMddHHmmss}_{Path.GetFileName(dialog.FileName)}";
            string destinationPath = Path.Combine(reportsDir, storedName);
            File.Copy(dialog.FileName, destinationPath, true);

            SqlDataAccess.SaveLabReportSubmission(
                selectedLab.LabWorkID,
                UserManager.GetCurrentUserId(),
                fileInfo.Name,
                storedName,
                destinationPath,
                (int)Math.Ceiling(fileInfo.Length / 1024.0),
                null);

            LoadLabsData();
            StatusText.Text = $"Статус: Отчет по лабораторной \"{selectedLab.Title}\" загружен.";
            MessageBox.Show("Отчет успешно загружен.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void HandleDataUpdate()
        {
            try
            {
                // #10 ИСПРАВЛЕНО: При обновлении данных сбрасываем кэш вопросов,
                // иначе студент может пройти тест со старыми вопросами после правки преподавателем
                _questionsCache.Clear();
                _questionCountCache.Clear();
                _searchCache.Clear();
                _allTestOptions = null;

                // 1. Перезагрузка TreeView
                LoadTopicsAndBuildTree();

                // 2. Перезагрузка списка тестов с актуальным количеством вопросов
                PopulateTestSelectionListBox();
                LoadLabsData();

                // 3. Проверка текущей темы
                if (currentTopic != null && !allTopics.Any(t => t.TopicID == currentTopic.TopicID))
                {
                    currentTopic = null;
                    MainContentHost.Content = null;
                    StatusText.Text = "Статус: Тема была удалена, контент сброшен.";
                }

                // 4. Если тест в процессе, возможно, нужно его прервать
                if (isTestInProgress)
                {
                    var result = MessageBox.Show("Данные тестов были изменены. Завершить текущий тест?",
                        "Изменение данных", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        ExitTestButton_Click(null, null);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        protected override void OnClosed(EventArgs e)
        {
            Computer_networks.Data.DataMessenger.DataChanged -= HandleDataUpdate;

            base.OnClosed(e);
        }

        private void StatisticsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void AchievementsTabItem_GotFocus(object sender, RoutedEventArgs e)
        {
            LoadAchievements();
        }

        private void LoadAchievements()
        {
            int userId = UserManager.GetCurrentUserId();
            if (userId <= 0)
            {
                AchievementsListBox.ItemsSource = null;
                return;
            }

            var achievements = SqlDataAccess.GetAllAchievementsWithStatus(userId);
            AchievementsListBox.ItemsSource = achievements;
        }



        private void CheckAndAwardAchievements()
        {
            int userId = UserManager.GetCurrentUserId();
            if (userId <= 0) return;

            // Достижения только для студентов
            if (!UserManager.CurrentUser.IsStudent) return;

            try
            {
                var beforeAchievements = SqlDataAccess.GetUserAchievements(userId)
                    .Select(a => a.AchievementID)
                    .ToHashSet();

                var testStats = SqlDataAccess.GetTestStatisticsByUserId(userId);
                int testsPassed = testStats?.Sum(t => t.Attempts) ?? 0;

                int perfectTests = 0;
                try
                {
                    using (var connection = new SqlConnection(SqlDataAccess.ConnectionString))
                        perfectTests = connection.QueryFirstOrDefault<int>(
                            "SELECT COUNT(DISTINCT TopicID) FROM TestResults WHERE UserID=@UserID AND CorrectAnswers=TotalQuestions",
                            new { UserID = userId });
                }
                catch (Exception ex) { Debug.WriteLine($"[CheckAchievements] perfectTests: {ex.Message}"); }

                int bookmarksCount = SqlDataAccess.GetAllBookmarksByUserId(userId)?.Count ?? 0;
                int topicsRead = SqlDataAccess.GetAllUserProgress(userId)?.Count ?? 0;

                int notesCreated = 0;
                try
                {
                    using (var connection = new SqlConnection(SqlDataAccess.ConnectionString))
                        notesCreated = connection.QueryFirstOrDefault<int>(
                            "SELECT COUNT(*) FROM TopicNotes WHERE UserID=@UserID",
                            new { UserID = userId });
                }
                catch (Exception ex) { Debug.WriteLine($"[CheckAchievements] notesCreated: {ex.Message}"); }

                int glossaryOpened = 0;
                try
                {
                    using (var connection = new SqlConnection(SqlDataAccess.ConnectionString))
                        glossaryOpened = connection.QueryFirstOrDefault<int>(
                            "SELECT ISNULL(GlossaryOpenCount, 0) FROM UserSettings WHERE UserID=@UserID",
                            new { UserID = userId });
                }
                catch (Exception ex) { Debug.WriteLine($"[CheckAchievements] glossaryOpened: {ex.Message}"); }

                int currentStreak = 0;
                try
                {
                    using (var connection = new SqlConnection(SqlDataAccess.ConnectionString))
                        currentStreak = connection.QueryFirstOrDefault<int>(
                            "SELECT CurrentStreak FROM Users WHERE UserID=@UserID",
                            new { UserID = userId });
                }
                catch (Exception ex) { Debug.WriteLine($"[CheckAchievements] currentStreak: {ex.Message}"); }

                var allAchievements = SqlDataAccess.LoadData<Achievement>("SELECT * FROM Achievements");
                var newlyEarnedAchievements = new List<Achievement>();

                foreach (var ach in allAchievements)
                {
                    if (beforeAchievements.Contains(ach.AchievementID)) continue;

                    bool earned = false;
                    try
                    {
                        switch (ach.ConditionType)
                        {
                            case "tests_taken": earned = testsPassed >= ach.ConditionValue; break;
                            case "bookmarks": earned = bookmarksCount >= ach.ConditionValue; break;
                            case "topics_read": earned = topicsRead >= ach.ConditionValue; break;
                            case "perfect_score":
                            case "perfect_streak": earned = perfectTests >= ach.ConditionValue; break;
                            case "streak": earned = currentStreak >= ach.ConditionValue; break;
                            case "notes": earned = notesCreated >= ach.ConditionValue; break;
                            case "glossary_views":
                                Debug.WriteLine($"[CheckAchievements] glossary_views: открыто={glossaryOpened}, нужно={ach.ConditionValue}");
                                earned = glossaryOpened >= ach.ConditionValue; break;
                            case "course_complete":
                                earned = SqlDataAccess.GetCompletedCoursesCount(userId) >= ach.ConditionValue; break;
                            case "courses_joined":
                                earned = SqlDataAccess.GetCoursesJoinedCount(userId) >= ach.ConditionValue; break;
                            case "code_views":
                                earned = SqlDataAccess.GetCodeViewsCount(userId) >= ach.ConditionValue; break;
                            case "night_session":
                                earned = SqlDataAccess.GetNightSessionsCount(userId) >= ach.ConditionValue; break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CheckAchievements] {ach.ConditionType} ОШИБКА: {ex.Message}");
                    }

                    if (earned)
                    {
                        SqlDataAccess.AwardAchievement(userId, ach.AchievementID);
                        newlyEarnedAchievements.Add(ach);
                    }
                }

                ShowAchievementNotifications(newlyEarnedAchievements);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CheckAchievements] ОБЩАЯ ОШИБКА: {ex.Message}");
            }
        }
        // Исправленный метод — защита от null
        private async void ShowAchievementNotifications(List<Achievement> achievements)
        {
            if (achievements == null || achievements.Count == 0) return;

            foreach (var ach in achievements)
            {
                var notification = new NotificationWindow(
                    ach.IconPath ?? "",           // если null — передаём пустую строку
                    ach.Title ?? "Достижение",    // если null — дефолтный текст
                    ach.Description ?? "",        // если null — пустая строка
                    ach.XP
                );
                notification.Show();
                await System.Threading.Tasks.Task.Delay(2000);
            }
        }
        private void LeaderboardButton_Click(object sender, RoutedEventArgs e)
        {
            var leaderboardWindow = new Views.LeaderboardWindow();
            leaderboardWindow.Owner = this;
            leaderboardWindow.ShowDialog();
        }

        private void CourseSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CourseSelector.SelectedValue is int courseId && courseId != SqlDataAccess.CurrentCourseId)
            {
                StatusText.Text = "Загрузка...";

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // ОЧИЩАЕМ КЭШ ПРИ СМЕНЕ КУРСА
                        ClearCache(); // Используем новый метод

                        SqlDataAccess.CurrentCourseId = courseId;

                        LoadTopicsAndBuildTree();
                        PopulateTestSelectionListBox();
                        LoadProgressData();
                        LoadBookmarks();
                        LoadLabsData();
                        // Пересоздаём AdminPanel чтобы статистика обновилась для нового курса
                        if (AdminContent != null && AdminContent.Content != null)
                            AdminContent.Content = new Views.AdminPanel();

                        StatusText.Text = $"Статус: Выбран курс {CourseSelector.Text}";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при смене курса: {ex.Message}");
                        StatusText.Text = "Статус: Ошибка загрузки";
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }


        private void AdminSettingsTabItem_GotFocus(object sender, RoutedEventArgs e)
        {
            // ИСПРАВЛЕНИЕ: GotFocus всплывает от дочерних элементов (кнопок внутри AdminPanel).
            // Раньше каждый клик на кнопку пересоздавал AdminPanel — кнопка уничтожалась
            // раньше, чем успевал сработать её Click-обработчик.
            // Теперь создаём панель только один раз — если Content уже есть, выходим.
            if (AdminContent.Content != null)
                return;

            if (UserManager.IsLoggedIn && (UserManager.CurrentUser.RoleID == 1 || UserManager.CurrentUser.RoleID == 2))
            {
                AdminContent.Content = new Views.AdminPanel();
            }
            else
            {
                AdminContent.Content = null;
                MainTabControl.SelectedIndex = 0;
            }
        }        // ========== МЕТОДЫ ДЛЯ РАБОТЫ С ОКНОМ ==========

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
        // #15 ИСПРАВЛЕНО: Добавлен обработчик кнопки минимизации
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Normal)
                WindowState = WindowState.Maximized;
            else
                WindowState = WindowState.Normal;
        }
        // #9 ИСПРАВЛЕНО: Убрана клавиша Key.F — она закрывала приложение при вводе текста в поиск.
        // Escape оставлен только для возможного будущего использования (сейчас без действия).
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Намеренно пусто: Escape/F4 обрабатываются ОС стандартно.
            // F1 и F2 обрабатываются в MainWindow_KeyDown.
        }
        // =============================================
        // МЕТОДЫ ДЛЯ РЕСУРСОВ ТЕМЫ (ФАЙЛЫ И ССЫЛКИ)
        // =============================================

        private void LoadTopicResources(int topicId)
        {
            try
            {
                // Загружаем файлы
                var attachments = SqlDataAccess.GetAttachmentsByTopic(topicId);
                TopicAttachmentsList.ItemsSource = attachments;

                // Загружаем ссылки
                var links = SqlDataAccess.GetLinksByTopic(topicId);
                TopicLinksList.ItemsSource = links;

                // Показываем/скрываем сообщение "нет ресурсов"
                if (NoResourcesText != null)
                {
                    NoResourcesText.Visibility = (!attachments.Any() && !links.Any())
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Resources] Ошибка загрузки: {ex.Message}");
            }
        }

        private void Attachment_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is TopicAttachment attachment)
            {
                try
                {
                    if (!System.IO.File.Exists(attachment.FullPath))
                    {
                        MessageBox.Show("Файл не найден на диске.", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = attachment.FullPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось открыть файл: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Link_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is TopicLink link)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = link.URL,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось открыть ссылку: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
    // Добавьте этот класс в конец файла
    public static class DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
        {
            if (dictionary == null) return defaultValue;
            return dictionary.TryGetValue(key, out TValue value) ? value : defaultValue;
        }
    }

}