using System;

namespace Computer_networks.Models
{
    public class AdminLog
    {
        public int LogID { get; set; }
        public int AdminID { get; set; }
        public string ActionType { get; set; }
        public int? TargetID { get; set; }
        public string Details { get; set; }
        public DateTime Timestamp { get; set; }
    }
}