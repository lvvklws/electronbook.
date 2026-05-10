using System;

namespace Computer_networks.Models
{
    public class UserAchievement
    {
        public int UserAchievementID { get; set; }
        public int UserID { get; set; }
        public int AchievementID { get; set; }
        public DateTime EarnedDate { get; set; }

        // из JOIN
        public string Title { get; set; }
        public string Description { get; set; }
        public string IconEmoji { get; set; }
        public int XP { get; set; }

        public string FormattedDate => EarnedDate.ToString("dd.MM.yyyy");
    }
}