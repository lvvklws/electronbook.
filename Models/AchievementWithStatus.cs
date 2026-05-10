using System;

namespace Computer_networks.Models
{
    public class AchievementWithStatus
    {
        public int AchievementID { get; set; }
        public string Title { get; set; }

        public string IconPath { get; set; }
        public string Description { get; set; }
        public string IconEmoji { get; set; }
        public int XP { get; set; }
        public string ConditionType { get; set; }
        public int ConditionValue { get; set; }
        public bool IsUnlocked { get; set; }
        public DateTime? UnlockedDate { get; set; }
        public int CurrentProgress { get; set; }
        public int ProgressPercent => (int)((double)CurrentProgress / ConditionValue * 100);
        public string ProgressText => $"{CurrentProgress}/{ConditionValue}";
    }
}