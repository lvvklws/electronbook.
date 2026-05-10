using System.ComponentModel;

namespace Computer_networks.Models
{
    /// <summary>
    /// Вспомогательная UI-модель для выбора студентов в GroupEditorWindow.
    /// НЕ наследует User — это отдельный класс только для отображения.
    /// Реализует INotifyPropertyChanged, чтобы CheckBox обновлял счётчик в реальном времени.
    /// </summary>
    public class StudentWithSelection : INotifyPropertyChanged
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
                // Уведомляем подписчиков (GroupEditorWindow обновит счётчик)
                SelectionChanged?.Invoke(this, System.EventArgs.Empty);
            }
        }

        public event System.EventHandler SelectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
