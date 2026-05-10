using Computer_networks.Data;
using Computer_networks.Models;
using Computer_networks.Utilities;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Computer_networks.ViewModels
{
    public class AdminUsersViewModel : ViewModelBase
    {
        public bool CanManageUsers => UserManager.CanManageUsers();
        public bool IsTeacherMode => UserManager.CurrentUser?.IsTeacher == true;

        private ObservableCollection<User> _users;
        public ObservableCollection<User> Users
        {
            get => _users;
            set { _users = value; OnPropertyChanged(nameof(Users)); }
        }

        private User _selectedUser;
        public User SelectedUser
        {
            get => _selectedUser;
            set
            {
                _selectedUser = value;
                OnPropertyChanged(nameof(SelectedUser));
                OnPropertyChanged(nameof(IsSelectedUserUnverified));
            }
        }

        public bool IsSelectedUserUnverified =>
            SelectedUser != null && !SelectedUser.IsEmailVerified;

        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand VerifyCommand { get; }
        public ICommand RefreshCommand { get; }

        public AdminUsersViewModel()
        {
            LoadUsers();

            UpdateCommand = new RelayCommand(
                _ => SaveChanges(),
                _ => CanManageUsers && SelectedUser != null);

            DeleteCommand = new RelayCommand(
                _ => DeleteUser(),
                _ => CanManageUsers
                     && SelectedUser != null
                     && SelectedUser.UserID != UserManager.GetCurrentUserId()
                     && !SelectedUser.IsAdmin);

            VerifyCommand = new RelayCommand(
                _ => ManuallyVerifyUser(),
                _ => CanManageUsers && SelectedUser != null && !SelectedUser.IsEmailVerified);

            RefreshCommand = new RelayCommand(_ => LoadUsers());
        }

        private void LoadUsers()
        {
            try
            {
                var users = SqlDataAccess.GetAllUsers();
                Users = new ObservableCollection<User>(users);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Не удалось загрузить список пользователей:\n{ex.Message}",
                    "Ошибка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        private void SaveChanges()
        {
            if (SelectedUser == null) return;
            try
            {
                SqlDataAccess.UpdateUser(SelectedUser);
                System.Windows.MessageBox.Show("Изменения сохранены.", "Успех");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private void DeleteUser()
        {
            if (SelectedUser == null) return;
            var result = System.Windows.MessageBox.Show(
                $"Удалить пользователя \u00ab{SelectedUser.Username}\u00bb?",
                "Подтверждение",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    SqlDataAccess.DeleteUser(SelectedUser.UserID);
                    Users.Remove(SelectedUser);
                    SelectedUser = null;
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Ошибка: {ex.Message}");
                }
            }
        }

        private void ManuallyVerifyUser()
        {
            if (SelectedUser == null) return;
            var result = System.Windows.MessageBox.Show(
                $"Вручную подтвердить аккаунт \u00ab{SelectedUser.Username}\u00bb?\n\nПользователь сможет войти без подтверждения email.",
                "Подтверждение",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            if (result != System.Windows.MessageBoxResult.Yes) return;
            try
            {
                SqlDataAccess.ManuallyVerifyUser(SelectedUser.UserID);
                SelectedUser.IsEmailVerified = true;
                int idx = Users.IndexOf(SelectedUser);
                if (idx >= 0) { var u = SelectedUser; Users.RemoveAt(idx); Users.Insert(idx, u); SelectedUser = Users[idx]; }
                OnPropertyChanged(nameof(IsSelectedUserUnverified));
                System.Windows.MessageBox.Show(
                    $"Аккаунт \u00ab{SelectedUser.Username}\u00bb подтверждён.",
                    "Готово", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }
    }
}