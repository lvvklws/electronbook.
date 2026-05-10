using System;

namespace Computer_networks.Models
{
    public class UserProgress
    {
        public int ProgressID { get; set; }
        public int UserID { get; set; }
        public int TopicID { get; set; }
        public bool IsRead { get; set; }
        public DateTime? LastViewedAt { get; set; }
        public int TimeSpentSeconds { get; set; }
        public int CourseID { get; set; } = 1;
    }
}