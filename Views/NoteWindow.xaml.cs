using System.Windows;
using System.Windows.Input;
using Computer_networks.Data;
using Computer_networks.Models;
using Computer_networks.Utilities;
using System.ComponentModel;
using System.Windows.Threading;
using System;

namespace Computer_networks.Views
{
    public partial class NoteWindow : Window
    {
        private bool _isChanged = false;
        private string _originalText = "";
        private DispatcherTimer _autoSaveTimer;
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();

            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
                SaveButton_Click(null, null);

            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
                SaveButton_Click(null, null);
        }
        private int _topicId;
        private int _userId;
        private Topic _topic;

        public NoteWindow(int topicId)
        {
            InitializeComponent();

            _topicId = topicId;
            _userId = UserManager.GetCurrentUserId();

            LoadTopic();
            LoadNote();

            // Таймер автосохранения
            _autoSaveTimer = new DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromSeconds(2);
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;

            // Отслеживание изменений
            NoteTextBox.TextChanged += (s, e) =>
            {
                _isChanged = NoteTextBox.Text != _originalText;

                _autoSaveTimer.Stop();
                _autoSaveTimer.Start();
            };
        }

        private void LoadTopic()
        {
            _topic = SqlDataAccess.GetTopicById(_topicId);
            if (_topic != null)
            {
                TopicTitleText.Text = $"Тема: {_topic.Title}";
            }
        }

        private void LoadNote()
        {
            var note = SqlDataAccess.GetTopicNote(_userId, _topicId);

            if (note != null)
            {
                NoteTextBox.Text = note.NoteText;
                _originalText = note.NoteText;
            }
            else
            {
                _originalText = "";
            }

            _isChanged = false;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveNote();

            MessageBox.Show("Заметка сохранена.",
                            "Готово",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }
        private void SaveNote()
        {
            string noteText = NoteTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(noteText))
            {
                SqlDataAccess.DeleteTopicNote(_userId, _topicId);
            }
            else
            {
                SqlDataAccess.SaveTopicNote(_userId, _topicId, noteText);
            }

            _originalText = NoteTextBox.Text;
            _isChanged = false;
        }
        private void AutoSaveTimer_Tick(object sender, EventArgs e)
        {
            _autoSaveTimer.Stop();

            if (_isChanged)
            {
                SaveNote();
            }
        }
        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isChanged)
            {
                var result = MessageBox.Show(
                    "Сохранить изменения?",
                    "Подтверждение",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                
                if (result == MessageBoxResult.Yes)
                {
                    SaveNote();
                }
            }

            base.OnClosing(e);
        }
    }
}