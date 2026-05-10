using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Computer_networks.Data;
using Computer_networks.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Microsoft.Win32;

namespace Computer_networks.Views
{
    public partial class AdminContentWindow : Window, INotifyPropertyChanged
    {
        private ObservableCollection<Topic> _topics;
        private Topic _selectedTopic;
        private Topic _originalTopic;
        private bool _hasUnsavedChanges = false;
        private const string ApplicationTitle = "Администрирование контента";
        private int _currentCourseId;

        public event PropertyChangedEventHandler PropertyChanged;

        public int TopicsCount => _topics?.Count ?? 0;
        public bool HasTopics => TopicsCount > 0;
        public bool CanDeleteTopic => _selectedTopic != null && HasTopics;

        public bool HasUnsavedChanges
        {
            get { return _hasUnsavedChanges; }
            set
            {
                if (_hasUnsavedChanges != value)
                {
                    _hasUnsavedChanges = value;
                    NotifyPropertyChanged(nameof(HasUnsavedChanges));
                }
            }
        }

        public AdminContentWindow(int courseId)
        {
            InitializeComponent();
            _currentCourseId = courseId;
            DataContext = this;
            LoadCourses();
            LoadTopics();
            SetupKeyboardShortcuts();
        }

        public AdminContentWindow() : this(2) { }

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
                System.Windows.MessageBox.Show($"Ошибка загрузки курсов: {ex.Message}", ApplicationTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CourseSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CourseSelector.SelectedValue is int courseId && courseId != _currentCourseId)
            {
                if (HasUnsavedChanges)
                {
                    var result = System.Windows.MessageBox.Show(
                        "Есть несохраненные изменения. Сохранить перед сменой курса?",
                        "Несохраненные изменения",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        SaveCurrentTopic();
                    }
                    else if (result == MessageBoxResult.Cancel)
                    {
                        CourseSelector.SelectedValue = _currentCourseId;
                        return;
                    }
                }

                _currentCourseId = courseId;
                LoadTopics();
            }
        }

        private void SetupKeyboardShortcuts()
        {
            this.KeyDown += (s, e) =>
            {
                if (e.KeyboardDevice.IsKeyDown(Key.LeftCtrl) || e.KeyboardDevice.IsKeyDown(Key.RightCtrl))
                {
                    switch (e.Key)
                    {
                        case Key.S:
                            SaveTopicButton_Click(null, null);
                            e.Handled = true;
                            break;
                        case Key.N:
                            AddRootTopicButton_Click(null, null);
                            e.Handled = true;
                            break;
                        case Key.Delete:
                            if (CanDeleteTopic)
                                DeleteTopicButton_Click(null, null);
                            e.Handled = true;
                            break;
                    }
                }
            };
        }

        private void LoadTopics()
        {
            try
            {
                var allTopics = SqlDataAccess.GetAllTopics(_currentCourseId);

                var rootTopics = allTopics.Where(t => t.ParentTopicID == null)
                                         .OrderBy(t => t.OrderIndex)
                                         .ToList();

                foreach (var rootTopic in rootTopics)
                {
                    rootTopic.Children.Clear();
                    var children = allTopics.Where(t => t.ParentTopicID == rootTopic.TopicID)
                                           .OrderBy(t => t.OrderIndex)
                                           .ToList();
                    foreach (var child in children)
                    {
                        rootTopic.Children.Add(child);
                    }
                }

                _topics = new ObservableCollection<Topic>(rootTopics);
                TopicsTreeView.ItemsSource = _topics;

                UpdateStatus();

                if (_topics.Any())
                {
                    if (_selectedTopic != null)
                    {
                        var currentTopic = FindTopicInTree(_topics, _selectedTopic.TopicID);
                        if (currentTopic != null)
                        {
                            SelectTreeViewItem(currentTopic);
                        }
                    }

                    if (TopicsTreeView.SelectedItem == null)
                    {
                        SelectTreeViewItem(_topics.First());
                    }
                }
                else
                {
                    ClearEditor();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка загрузки тем: {ex.Message}", ApplicationTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectTreeViewItem(Topic topic)
        {
            var itemContainer = GetTreeViewItem(TopicsTreeView, topic);
            if (itemContainer != null)
            {
                itemContainer.IsSelected = true;
                itemContainer.BringIntoView();
            }
        }

        private TreeViewItem GetTreeViewItem(ItemsControl parent, Topic topic)
        {
            if (parent == null) return null;

            foreach (var item in parent.Items)
            {
                var treeViewItem = parent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (treeViewItem != null)
                {
                    if (treeViewItem.DataContext == topic)
                        return treeViewItem;

                    var foundInChildren = GetTreeViewItem(treeViewItem, topic);
                    if (foundInChildren != null)
                        return foundInChildren;
                }
            }
            return null;
        }

        private Topic FindTopicInTree(ObservableCollection<Topic> topics, int topicId)
        {
            foreach (var topic in topics)
            {
                if (topic.TopicID == topicId)
                    return topic;

                var foundInChildren = FindTopicInTree(topic.Children, topicId);
                if (foundInChildren != null)
                    return foundInChildren;
            }
            return null;
        }

        private void UpdateStatus()
        {
            StatusText.Text = _selectedTopic != null ?
                $"Редактирование: {_selectedTopic.Title}" :
                "Выберите тему для редактирования";

            NotifyPropertyChanged(nameof(TopicsCount));
            NotifyPropertyChanged(nameof(HasTopics));
            NotifyPropertyChanged(nameof(CanDeleteTopic));
            NotifyPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void TopicsTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (HasUnsavedChanges && _selectedTopic != null)
            {
                var result = System.Windows.MessageBox.Show(
                    "Есть несохраненные изменения. Сохранить перед переходом?",
                    "Несохраненные изменения",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SaveCurrentTopic();
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    SelectTreeViewItem(_selectedTopic);
                    return;
                }
            }

            var newSelectedTopic = TopicsTreeView.SelectedItem as Topic;

            if (newSelectedTopic?.TopicID == _selectedTopic?.TopicID)
                return;

            _selectedTopic = newSelectedTopic;

            if (_selectedTopic != null)
            {
                _originalTopic = new Topic
                {
                    Title = _selectedTopic.Title,
                    Content = _selectedTopic.Content
                };

                TitleTextBox.Text = _selectedTopic.Title;

                // Загружаем контент в TextBox
                LoadContentToTextBox(_selectedTopic.Content);

                HasUnsavedChanges = false;
                LoadAttachments(_selectedTopic.TopicID);
                LoadLinks(_selectedTopic.TopicID);
            }
            else
            {
                ClearEditor();
            }

            UpdateStatus();
        }

        // Новый метод для загрузки текста
        private void LoadContentToTextBox(string content)
        {
            ContentTextBox.Text = content ?? "";
        }

        private void ClearEditor()
        {
            TitleTextBox.Text = "";
            ContentTextBox.Text = "";
            HasUnsavedChanges = false;
            _originalTopic = null;
            if (AttachmentsList != null) AttachmentsList.ItemsSource = null;
            if (LinksList != null) LinksList.ItemsSource = null;
            if (NoAttachmentsText != null) NoAttachmentsText.Visibility = Visibility.Visible;
            if (NoLinksText != null) NoLinksText.Visibility = Visibility.Visible;
        }

        // Новый метод для отслеживания изменений
        private void ContentTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedTopic != null && _originalTopic != null)
            {
                HasUnsavedChanges = TitleTextBox.Text != _originalTopic.Title ||
                                   ContentTextBox.Text != _originalTopic.Content;

                if (HasUnsavedChanges)
                {
                    StatusText.Text = $"Редактирование: {_selectedTopic.Title} *";
                }
                else
                {
                    StatusText.Text = $"Редактирование: {_selectedTopic.Title}";
                }
            }
        }

        // =============================================
        // ВЛОЖЕНИЯ
        // =============================================

        private void LoadAttachments(int topicId)
        {
            try
            {
                var attachments = SqlDataAccess.GetAttachmentsByTopic(topicId);
                AttachmentsList.ItemsSource = attachments;
                NoAttachmentsText.Visibility = attachments.Any()
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Attachments] Ошибка: {ex.Message}");
            }
        }

        private void AddAttachment_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTopic == null)
            {
                System.Windows.MessageBox.Show("Сначала выберите тему.", ApplicationTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Выберите файл",
                Filter = "Все файлы|*.pdf;*.jpg;*.jpeg;*.png;*.mp4;*.avi|" +
                         "PDF|*.pdf|Картинки|*.jpg;*.jpeg;*.png;*.gif|Видео|*.mp4;*.avi;*.mkv",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var fileInfo = new FileInfo(dialog.FileName);

                if (fileInfo.Length > 50 * 1024 * 1024)
                {
                    System.Windows.MessageBox.Show("Файл слишком большой. Максимум 50 МБ.", ApplicationTitle,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string ext = fileInfo.Extension.ToLower();
                string fileType = ext switch
                {
                    ".pdf" => "pdf",
                    ".jpg" or ".jpeg" or ".png" or ".gif" => "image",
                    ".mp4" or ".avi" or ".mkv" => "video",
                    _ => "other"
                };

                string storedName = $"{Guid.NewGuid()}{ext}";
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments");
                Directory.CreateDirectory(folder);

                string destination = Path.Combine(folder, storedName);
                File.Copy(dialog.FileName, destination);

                SqlDataAccess.AddAttachment(
                    _selectedTopic.TopicID,
                    fileInfo.Name,
                    storedName,
                    fileType,
                    (int)(fileInfo.Length / 1024),
                    Utilities.UserManager.CurrentUser?.UserID ?? 0
                );

                LoadAttachments(_selectedTopic.TopicID);
                System.Windows.MessageBox.Show("Файл прикреплён!", ApplicationTitle,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка: {ex.Message}", ApplicationTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenAttachment_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Models.TopicAttachment att)
            {
                if (!File.Exists(att.FullPath))
                {
                    System.Windows.MessageBox.Show("Файл не найден на диске.", ApplicationTitle,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = att.FullPath,
                    UseShellExecute = true
                });
            }
        }

        private void DeleteAttachment_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not int id) return;

            var r = System.Windows.MessageBox.Show("Удалить вложение?", ApplicationTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            try
            {
                var all = SqlDataAccess.GetAttachmentsByTopic(_selectedTopic.TopicID);
                var att = all.FirstOrDefault(a => a.AttachmentID == id);

                SqlDataAccess.DeleteAttachment(id);

                if (att != null && File.Exists(att.FullPath))
                    File.Delete(att.FullPath);

                LoadAttachments(_selectedTopic.TopicID);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка удаления: {ex.Message}", ApplicationTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =============================================
        // ССЫЛКИ
        // =============================================

        private void LoadLinks(int topicId)
        {
            try
            {
                var links = SqlDataAccess.GetLinksByTopic(topicId);
                LinksList.ItemsSource = links;
                NoLinksText.Visibility = links.Any()
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Links] Ошибка: {ex.Message}");
            }
        }

        private void AddLink_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTopic == null)
            {
                System.Windows.MessageBox.Show("Сначала выберите тему.", ApplicationTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new AddLinkDialog { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    SqlDataAccess.AddTopicLink(
                        _selectedTopic.TopicID,
                        dialog.LinkTitle,
                        dialog.LinkUrl,
                        dialog.LinkDesc,
                        Utilities.UserManager.CurrentUser?.UserID ?? 0
                    );

                    LoadLinks(_selectedTopic.TopicID);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Ошибка: {ex.Message}", ApplicationTitle,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenLink_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is string url)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
        }

        private void DeleteLink_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not int id) return;

            var r = System.Windows.MessageBox.Show("Удалить ссылку?", ApplicationTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            try
            {
                SqlDataAccess.DeleteTopicLink(id);
                LoadLinks(_selectedTopic.TopicID);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка удаления: {ex.Message}", ApplicationTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =============================================
        // ОСНОВНЫЕ МЕТОДЫ
        // =============================================

        private void AddRootTopicButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var newTopic = new Topic
                {
                    Title = "Новая главная тема",
                    Content = "Содержание главной темы",
                    ParentTopicID = null,
                    OrderIndex = _topics.Count(t => t.ParentTopicID == null) + 1,
                    CourseID = _currentCourseId
                };

                int newId = SqlDataAccess.InsertTopic(newTopic);
                LoadTopics();

                System.Windows.MessageBox.Show("Главная тема создана!", ApplicationTitle,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка создания главной темы: {ex.Message}", ApplicationTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddSubTopicButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTopic == null)
            {
                System.Windows.MessageBox.Show("Выберите тему, к которой нужно добавить подтему", ApplicationTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_selectedTopic.ParentTopicID.HasValue)
            {
                System.Windows.MessageBox.Show("Нельзя создавать подтему внутри подтемы!\n\nВыберите главную тему (без родителя).",
                               ApplicationTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var newSubTopic = new Topic
                {
                    Title = "Новая подтема",
                    Content = "Содержание подтемы",
                    ParentTopicID = _selectedTopic.TopicID,
                    OrderIndex = GetNextSubTopicOrder(_selectedTopic.TopicID),
                    CourseID = _currentCourseId
                };

                int newId = SqlDataAccess.InsertTopic(newSubTopic);
                LoadTopics();

                System.Windows.MessageBox.Show("Подтема создана!", ApplicationTitle,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка создания подтемы: {ex.Message}", ApplicationTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int GetNextSubTopicOrder(int parentTopicId)
        {
            var allTopics = SqlDataAccess.GetAllTopics(_currentCourseId);
            var subTopics = allTopics.Where(t => t.ParentTopicID == parentTopicId);
            return subTopics.Any() ? subTopics.Max(t => t.OrderIndex) + 1 : 1;
        }

        private void SaveTopicButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentTopic();
        }

        private void SaveCurrentTopic()
        {
            if (_selectedTopic == null)
            {
                System.Windows.MessageBox.Show("Выберите тему для сохранения", ApplicationTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                System.Windows.MessageBox.Show("Заголовок темы не может быть пустым", ApplicationTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TitleTextBox.Focus();
                return;
            }

            try
            {
                int currentTopicId = _selectedTopic.TopicID;

                _selectedTopic.Title = TitleTextBox.Text.Trim();
                _selectedTopic.Content = ContentTextBox.Text;

                SqlDataAccess.UpdateTopic(_selectedTopic);

                _originalTopic.Title = _selectedTopic.Title;
                _originalTopic.Content = _selectedTopic.Content;

                HasUnsavedChanges = false;
                DataMessenger.NotifyDataChanged();
                LoadTopics();

                var updatedTopic = FindTopicInTree(_topics, currentTopicId);
                if (updatedTopic != null)
                {
                    SelectTreeViewItem(updatedTopic);
                }

                UpdateStatus();
                System.Windows.MessageBox.Show("Изменения сохранены!", ApplicationTitle,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка сохранения: {ex.Message}", ApplicationTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteTopicButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTopic == null || !CanDeleteTopic)
            {
                System.Windows.MessageBox.Show("Выберите тему для удаления", ApplicationTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Вы уверены, что хотите удалить тему:\n\"{_selectedTopic.Title}\"?\n\nЭто действие необратимо.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    int topicIdToDelete = _selectedTopic.TopicID;
                    SqlDataAccess.DeleteTopic(topicIdToDelete);

                    LoadTopics();
                    DataMessenger.NotifyDataChanged();

                    if (_topics.Any())
                    {
                        SelectTreeViewItem(_topics.First());
                    }
                    else
                    {
                        ClearEditor();
                    }

                    System.Windows.MessageBox.Show("Тема успешно удалена!", ApplicationTitle,
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Ошибка удаления: {ex.Message}", ApplicationTitle,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ContentTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ContentTypeComboBox.SelectedIndex == 1)
            {
                ContentPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                ContentPanel.Visibility = Visibility.Visible;
            }
        }

        public void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (HasUnsavedChanges)
            {
                var result = System.Windows.MessageBox.Show(
                    "Есть несохраненные изменения. Сохранить перед выходом?",
                    "Несохраненные изменения",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SaveCurrentTopic();
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
            base.OnClosing(e);
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

        private void TitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedTopic != null && _originalTopic != null)
            {
                HasUnsavedChanges = TitleTextBox.Text != _originalTopic.Title ||
                                   ContentTextBox.Text != _originalTopic.Content;

                if (HasUnsavedChanges)
                {
                    StatusText.Text = $"Редактирование: {_selectedTopic.Title} *";
                }
                else
                {
                    StatusText.Text = $"Редактирование: {_selectedTopic.Title}";
                }
            }
        }

        // =============================================
        // МЕТОДЫ ФОРМАТИРОВАНИЯ
        // =============================================


        
    }
}