using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Computer_networks.Data;
using Computer_networks.Models;
using Computer_networks.Utilities;

namespace Computer_networks.Views
{
    public partial class LeaderboardWindow : Window
    {
        private ObservableCollection<LeaderboardItem> _leaderboardItems;
        private DispatcherTimer _refreshTimer;
        private int _totalParticipants;
        private int _currentUserRank;

        public LeaderboardWindow()
        {
            InitializeComponent();
            Loaded += LeaderboardWindow_Loaded;
            KeyDown += Window_KeyDown;
        }

        private void LeaderboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadLeaderboardData();
            StartAutoRefresh();
        }

        private void StartAutoRefresh()
        {
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _refreshTimer.Tick += (s, e) => RefreshData();
            _refreshTimer.Start();
        }

        private void LoadLeaderboardData()
        {
            try
            {
                // Используем существующий метод из SqlDataAccess
                var leaderboardData = SqlDataAccess.GetLeaderboard(50); // Получаем топ-50

                _leaderboardItems = new ObservableCollection<LeaderboardItem>();

                foreach (var entry in leaderboardData)
                {
                    var item = new LeaderboardItem
                    {
                        Rank = entry.Rank,
                        UserId = entry.UserID,
                        Username = entry.Username,
                        Initials = GetInitials(entry.Username),
                        Level = CalculateLevel(entry.TotalXP),
                        LevelProgress = CalculateLevelProgress(entry.TotalXP),
                        AchievementsCount = entry.AchievementsCount,
                        TotalXP = entry.TotalXP,
                        IsCurrentUser = (entry.UserID == UserManager.GetCurrentUserId())
                    };
                    _leaderboardItems.Add(item);
                }

                _totalParticipants = _leaderboardItems.Count;
                _currentUserRank = GetCurrentUserRank();

                LeaderboardListBox.ItemsSource = _leaderboardItems;
                DataContext = this;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetInitials(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return "??";

            return username.Length >= 2 ? username.Substring(0, 2).ToUpper() : username.ToUpper();
        }

        // Пороги XP для каждого уровня (уровень = индекс + 1)
        // Уровень 1: 0–19 XP, Уровень 2: 20–49, ..., Уровень 10: 500+
        private static readonly int[] LevelThresholds = { 0, 50, 100, 150, 200, 300, 400, 500, 800, int.MaxValue };

        private int CalculateLevel(int totalXP)
        {
            for (int i = LevelThresholds.Length - 2; i >= 0; i--)
            {
                if (totalXP >= LevelThresholds[i])
                    return i + 1;
            }
            return 1;
        }

        private int CalculateLevelProgress(int totalXP)
        {
            int level = CalculateLevel(totalXP);
            // Если максимальный уровень — прогресс 100%
            if (level >= LevelThresholds.Length - 1)
                return 100;

            int currentThreshold = LevelThresholds[level - 1];
            int nextThreshold = LevelThresholds[level];
            int xpInLevel = totalXP - currentThreshold;
            int xpNeeded = nextThreshold - currentThreshold;

            return xpNeeded > 0 ? (xpInLevel * 100 / xpNeeded) : 0;
        }

        private int GetCurrentUserRank()
        {
            if (_leaderboardItems == null) return 0;

            var currentUser = _leaderboardItems.FirstOrDefault(x => x.IsCurrentUser);
            return currentUser?.Rank ?? 0;
        }

        private void RefreshData()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LoadLeaderboardData();
            }));
        }

        public int TotalParticipants
        {
            get => _totalParticipants;
            set
            {
                _totalParticipants = value;
                OnPropertyChanged(nameof(TotalParticipants));
            }
        }

        public int CurrentUserRank
        {
            get => _currentUserRank;
            set
            {
                _currentUserRank = value;
                OnPropertyChanged(nameof(CurrentUserRank));
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshData();
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
            else if (e.Key == Key.F5)
            {
                RefreshData();
                e.Handled = true;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class LeaderboardItem : INotifyPropertyChanged
    {
        private int _rank;
        private int _userId;
        private string _username;
        public string FullName => Username?.Replace("_", " ") ?? ""; private string _initials;
        private int _level;
        private int _levelProgress;
        private int _achievementsCount;
        private int _totalXP;
        private bool _isCurrentUser;

        public int Rank
        {
            get => _rank;
            set
            {
                _rank = value;
                OnPropertyChanged();
            }
        }

        public int UserId
        {
            get => _userId;
            set
            {
                _userId = value;
                OnPropertyChanged();
            }
        }

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged();
            }
        }

        public string Initials
        {
            get => _initials;
            set
            {
                _initials = value;
                OnPropertyChanged();
            }
        }

        public int Level
        {
            get => _level;
            set
            {
                _level = value;
                OnPropertyChanged();
            }
        }

        public int LevelProgress
        {
            get => _levelProgress;
            set
            {
                _levelProgress = value;
                OnPropertyChanged();
            }
        }

        public int AchievementsCount
        {
            get => _achievementsCount;
            set
            {
                _achievementsCount = value;
                OnPropertyChanged();
            }
        }

        public int TotalXP
        {
            get => _totalXP;
            set
            {
                _totalXP = value;
                OnPropertyChanged();
            }
        }

        public bool IsCurrentUser
        {
            get => _isCurrentUser;
            set
            {
                _isCurrentUser = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}