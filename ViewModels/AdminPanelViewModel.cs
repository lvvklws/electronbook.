using System.Windows.Input;
using Computer_networks.Models;
using Computer_networks.Utilities;

namespace Computer_networks.ViewModels
{
    public class AdminPanelViewModel : ViewModelBase
    {
        public void RefreshRoleInfo()
        {
            OnPropertyChanged(nameof(RoleDescription));
            OnPropertyChanged(nameof(RoleBadgeColor));
            OnPropertyChanged(nameof(PanelTitle));
            OnPropertyChanged(nameof(CanManageUsers));
            OnPropertyChanged(nameof(CanManageContent));
            OnPropertyChanged(nameof(CanManageTests));
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(IsTeacher));
            OnPropertyChanged(nameof(IsStudent));
        }

        // Существующие свойства
        public bool CanManageUsers => UserManager.CanManageUsers();
        public bool CanManageContent => UserManager.CanManageContent();

        // Добавьте новое свойство для тестов (если его нет в UserManager)
        public bool CanManageTests => UserManager.CanManageContent(); // или отдельный метод, если есть

        // Добавьте свойства для проверки конкретных ролей
        public bool IsAdmin => UserManager.CurrentUser?.IsAdmin == true;
        public bool IsTeacher => UserManager.CurrentUser?.IsTeacher == true;
        public bool IsStudent => UserManager.CurrentUser?.IsStudent == true;

        public string PanelTitle
        {
            get
            {
                if (UserManager.CurrentUser?.IsAdmin == true)
                    return "🔒 ПАНЕЛЬ АДМИНИСТРАТОРА";
                if (UserManager.CurrentUser?.IsTeacher == true)
                    return "📚 ПАНЕЛЬ ПРЕПОДАВАТЕЛЯ";
                return "⚙️ ПАНЕЛЬ УПРАВЛЕНИЯ";
            }
        }

        public string RoleDescription
        {
            get
            {
                if (UserManager.CurrentUser?.IsAdmin == true)
                    return "Администратор";
                if (UserManager.CurrentUser?.IsTeacher == true)
                    return "Преподаватель";
                return "Студент";
            }
        }

        public string RoleBadgeColor
        {
            get
            {
                if (UserManager.CurrentUser?.IsAdmin == true) return "#CC3333";
                if (UserManager.CurrentUser?.IsTeacher == true) return "#007ACC";
                return "#33AA33";
            }
        }

        public ICommand OpenContentCommand { get; private set; }
        public ICommand OpenTestsCommand { get; private set; }
        public ICommand OpenUsersCommand { get; private set; }

        public AdminPanelViewModel()
        {
            OpenUsersCommand = new RelayCommand(_ => OpenUsers(), _ => CanManageUsers);
            OpenContentCommand = new RelayCommand(_ => OpenContent(), _ => CanManageContent);
            OpenTestsCommand = new RelayCommand(_ => OpenTests(), _ => CanManageTests);
        }

        private void OpenUsers()
        {
            // Здесь будет логика открытия окна
            System.Windows.MessageBox.Show("Открытие окна пользователей");
        }

        private void OpenContent()
        {
            System.Windows.MessageBox.Show("Открытие окна контента");
        }

        private void OpenTests()
        {
            System.Windows.MessageBox.Show("Открытие окна тестов");
        }
    }
}