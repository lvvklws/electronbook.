using System.ComponentModel;
using System.Collections.ObjectModel;

namespace Computer_networks.Models
{
    public class Topic : INotifyPropertyChanged
    {
        public int TopicID { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public int? ParentTopicID { get; set; }
        public int OrderIndex { get; set; }

        public int CourseID { get; set; } = 0; // #6 #7 ИСПРАВЛЕНО: значение устанавливается через SqlDataAccess.CurrentCourseId

        // Добавляем свойства для иерархии
        public ObservableCollection<Topic> Children { get; set; } = new ObservableCollection<Topic>();
        public bool HasChildren => Children?.Count > 0;
        public bool IsRoot => !ParentTopicID.HasValue;

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public string Icon => ParentTopicID == null ? "📂" : "📄";

    }
}