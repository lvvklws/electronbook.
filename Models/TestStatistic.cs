using System;
using System.Collections.Generic;

namespace Computer_networks.Models
{
    public class TestStatistic
    {
        public int TopicID { get; set; }
        public string TopicTitle { get; set; }
        public int Attempts { get; set; }

        // Средний балл (AvgScore): Число от 0 до 100
        public decimal AvgScore { get; set; }

        // НОВОЕ: Лучший балл (BestScore): Число от 0 до 100
        public decimal BestScore { get; set; }


        public int CourseID { get; set; } = 1;
        public int TimeSpentSeconds { get; set; }

        public decimal ScorePercent => AvgScore / 100M;

        // Свойство для лучшего результата (BestScore / 100)
        public decimal BestScorePercent => BestScore / 100M;

        public int CorrectAnswers { get; set; }
        public int TotalQuestions { get; set; }
    }
}