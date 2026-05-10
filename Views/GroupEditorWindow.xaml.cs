using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Computer_networks.Data;
using Computer_networks.Models;
using System.Windows.Data;


namespace Computer_networks.Views
{
    public partial class GroupEditorWindow : Window
    {
        private ObservableCollection<StudentWithSelection> _allStudents;
        private Group _editingGroup;
        private ICollectionView _studentsView;

        private int _currentCourseId;
        private bool _isEditMode = false;

        public GroupEditorWindow(int courseId)
        {
            InitializeComponent();
            _currentCourseId = courseId;
            _isEditMode = false;
            LoadInitialData();
            WindowTitleText.Text = "📁 Создание новой группы";
        }

        public GroupEditorWindow(Group group, int courseId)
        {
            InitializeComponent();
            _editingGroup = group;
            _currentCourseId = courseId;
            _isEditMode = true;
            LoadInitialData();
            LoadGroupData();
            WindowTitleText.Text = $"✏️ Редактирование: {group.GroupName}";
        }

        private void LoadInitialData()
        {
            LoadCourses();
            LoadStudents();
        }

        private void LoadCourses()
        {
            try
            {
                var courses = SqlDataAccess.GetAllCourses();
                CourseCombo.ItemsSource = courses; // Имя из XAML 

                CourseCombo.SelectedValue = _isEditMode && _editingGroup.CourseID.HasValue
                    ? (object)_editingGroup.CourseID.Value
                    : (object)_currentCourseId;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка БД (Курсы): {ex.Message}");
            }
        }

        private void LoadStudents()
        {
            try
            {
                var students = SqlDataAccess.GetAllStudents();
                HashSet<int> selectedIds = new HashSet<int>();

                if (_isEditMode && _editingGroup != null)
                {
                    var groupStudents = SqlDataAccess.GetGroupStudents(_editingGroup.GroupID);
                    selectedIds = groupStudents.Select(s => s.UserID).ToHashSet();
                }

                var items = students.Select(s => new StudentWithSelection
                {
                    UserID = s.UserID,
                    Username = s.Username,
                    Email = s.Email,
                    IsSelected = selectedIds.Contains(s.UserID)
                }).ToList();

                _allStudents = new ObservableCollection<StudentWithSelection>(items);

                // Подписываемся на изменения для обновления счетчика
                foreach (var s in _allStudents)
                    s.SelectionChanged += (sdr, args) => UpdateSelectedCount();

                _studentsView = CollectionViewSource.GetDefaultView(_allStudents);
                StudentsList.ItemsSource = _studentsView;
                UpdateSelectedCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка БД (Студенты): {ex.Message}");
            }
        }

        private void UpdateSelectedCount()
        {
            if (_allStudents == null) return;
            int count = _allStudents.Count(s => s.IsSelected);
            SelectedCountText.Text = $"Выбрано: {count} студентов"; // [cite: 61]
            SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(GroupNameBox.Text) && count > 0;
        }

        private void LoadGroupData()
        {
            if (_editingGroup == null) return;
            GroupNameBox.Text = _editingGroup.GroupName;
            GroupDescBox.Text = _editingGroup.Description;
        }

        private void SearchStudentBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_studentsView == null) return;

            string search = SearchStudentBox.Text.Trim().ToLowerInvariant();

            _studentsView.Filter = obj =>
            {
                if (string.IsNullOrEmpty(search))
                    return true;

                var s = obj as StudentWithSelection;
                return s.Username.ToLowerInvariant().Contains(search)
                    || (s.Email?.ToLowerInvariant().Contains(search) == true);
            };
        }


        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var student in _allStudents) student.IsSelected = true;
            UpdateSelectedCount();
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var student in _allStudents) student.IsSelected = false;
            UpdateSelectedCount();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int? courseId = CourseCombo.SelectedValue as int?;
                string name = GroupNameBox.Text.Trim();
                string desc = GroupDescBox.Text?.Trim();
                var selectedIds = _allStudents.Where(s => s.IsSelected).Select(s => s.UserID).ToList();

                if (_isEditMode)
                {
                    SqlDataAccess.UpdateGroup(_editingGroup.GroupID, name, desc, courseId);
                    // Логика синхронизации состава...
                }
                else
                {
                    int newId = SqlDataAccess.CreateGroup(name, desc, courseId);
                    foreach (var id in selectedIds) SqlDataAccess.AddStudentToGroup(newId, id);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            }
        }

        private void GroupNameBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateSelectedCount();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
        private void DragWindow(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
        private void Window_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) Close(); }
    }
}