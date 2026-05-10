using System;

namespace Computer_networks.Models
{
    public class TestResult
    {
        public int ResultID { get; set; }
        public int UserID { get; set; }
        public int? TopicID { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public DateTime TestDate { get; set; }
    }
}