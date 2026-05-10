using System.Collections.Generic;

namespace Computer_networks.Models
{
    /// <summary>
    /// Статистика успеваемости группы
    /// </summary>
    public class GroupStatistics
    {
        public int GroupID { get; set; }                        // ID группы
        public string GroupName { get; set; }                   // Название группы
        public int StudentCount { get; set; }                   // Кол-во студентов

        // Общая статистика
        public double AvgScore { get; set; }                    // Средний балл по группе
        public int TotalAttempts { get; set; }                  // Всего попыток тестов
        public int CompletedTopics { get; set; }                // Всего тем изучено
        public int PerfectTests { get; set; }                   // Тестов на 100%

        // Детальная статистика по темам
        public List<TopicStat> TopicStats { get; set; }         // Статистика по каждой теме

        // Лучшие студенты группы
        public List<StudentStat> TopStudents { get; set; }      // Топ-5 студентов
    }

    /// <summary>
    /// Статистика по конкретной теме
    /// </summary>
    public class TopicStat
    {
        public string TopicTitle { get; set; }                  // Название темы
        public double GroupAvgScore { get; set; }               // Средний балл группы по теме
        public double OverallAvgScore { get; set; }             // Общий средний балл (для сравнения)
    }

    /// <summary>
    /// Статистика конкретного студента (для топа)
    /// </summary>
    public class StudentStat
    {
        public int UserID { get; set; }                          // ID студента
        public string Username { get; set; }                     // Логин
        public double AvgScore { get; set; }                     // Средний балл
        public int TotalAttempts { get; set; }                   // Всего попыток
    }
}