namespace Computer_networks.Models
{
    public class Achievement
    {
        public int AchievementID { get; set; }

        public string IconPath { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string IconEmoji { get; set; }
        public string ConditionType { get; set; }
        public int ConditionValue { get; set; }
        public int XP { get; set; }
    }
}