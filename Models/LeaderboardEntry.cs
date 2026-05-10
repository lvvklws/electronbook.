namespace Computer_networks.Models
{
    public class LeaderboardEntry
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public int TotalXP { get; set; }
        public int Level => TotalXP / 100 + 1;
        public int AchievementsCount { get; set; }
        public int Rank { get; set; }
        public bool IsCurrentUser { get; set; } = false;
    }
}