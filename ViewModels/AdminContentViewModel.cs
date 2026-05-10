using Computer_networks.Data;
using Computer_networks.Models;
using Computer_networks.Utilities;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace Computer_networks.ViewModels
{
    public class AdminContentViewModel : ViewModelBase
    {
        private ObservableCollection<Topic> _topics;
        private Topic _selectedTopic;
        private string _topicContentText;
        private string _topicTitle;

        public ObservableCollection<Topic> Topics
        {
            get => _topics;
            set => SetProperty(ref _topics, value);
        }

        public Topic SelectedTopic
        {
            get => _selectedTopic;
            set
            {
                if (_selectedTopic == value) return;

                _selectedTopic = value;
                OnPropertyChanged(nameof(SelectedTopic));

                if (value != null)
                {
                    _topicContentText = value.Content;
                    _topicTitle = value.Title;
                }
                else
                {
                    _topicContentText = string.Empty;
                    _topicTitle = string.Empty;
                }

                OnPropertyChanged(nameof(TopicContentText));
                OnPropertyChanged(nameof(TopicTitle));
            }
        }

        public string TopicContentText
        {
            get => _topicContentText;
            set => SetProperty(ref _topicContentText, value);
        }

        public string TopicTitle
        {
            get => _topicTitle;
            set => SetProperty(ref _topicTitle, value);
        }

        public ICommand SaveTopicCommand { get; }
        public ICommand NewTopicCommand { get; }
        public ICommand DeleteTopicCommand { get; }

        public AdminContentViewModel()
        {
            SaveTopicCommand = new RelayCommand(SaveTopic);
            NewTopicCommand = new RelayCommand(CreateNewTopic);
            DeleteTopicCommand = new RelayCommand(DeleteTopic);

            // Грубая загрузка данных
            try
            {
                var allTopics = SqlDataAccess.GetAllTopics();
                Topics = new ObservableCollection<Topic>(allTopics.OrderBy(t => t.OrderIndex));
                if (Topics.Any())
                    SelectedTopic = Topics.First();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}");
            }
        }

        private void SaveTopic(object parameter)
        {
            if (_selectedTopic == null) return;

            try
            {
                _selectedTopic.Content = _topicContentText;
                _selectedTopic.Title = _topicTitle;

                SqlDataAccess.UpdateTopic(_selectedTopic);
                MessageBox.Show("Сохранено!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private void CreateNewTopic(object parameter)
        {
            try
            {
                var newTopic = new Topic
                {
                    Title = "Новая тема",
                    Content = "Содержание новой темы",
                    OrderIndex = Topics.Count + 1
                };

                int newId = SqlDataAccess.InsertTopic(newTopic);

                // Грубо перезагружаем
                var allTopics = SqlDataAccess.GetAllTopics();
                Topics = new ObservableCollection<Topic>(allTopics.OrderBy(t => t.OrderIndex));
                SelectedTopic = Topics.FirstOrDefault(t => t.TopicID == newId);

                MessageBox.Show("Новая тема создана!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private void DeleteTopic(object parameter)
        {
            if (_selectedTopic == null) return;

            if (MessageBox.Show($"Удалить '{_selectedTopic.Title}'?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    SqlDataAccess.DeleteTopic(_selectedTopic.TopicID);

                    // Грубо перезагружаем
                    var allTopics = SqlDataAccess.GetAllTopics();
                    Topics = new ObservableCollection<Topic>(allTopics.OrderBy(t => t.OrderIndex));
                    SelectedTopic = Topics.FirstOrDefault();

                    MessageBox.Show("Удалено!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
            }
        }
    }
}