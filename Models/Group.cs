using System;

namespace Computer_networks.Models
{
    /// <summary>
    /// Модель учебной группы.
    /// Students намеренно убран — он никогда не заполнялся и давал StudentCount = 0.
    /// Вместо этого StudentCount заполняется напрямую из SQL (COUNT JOIN).
    /// </summary>
    public class Group
    {
        public int GroupID { get; set; }
        public string GroupName { get; set; }
        public string Description { get; set; }
        public int? CourseID { get; set; }
        public string CourseName { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Заполняется из БД через COUNT в запросе GetAllGroups.
        /// Больше не зависит от списка Students, который никогда не загружался.
        /// </summary>
        public int StudentCount { get; set; }

        public string DisplayInfo => $"{GroupName} ({StudentCount} студ.)";
        public string DisplayName => string.IsNullOrEmpty(CourseName)
            ? GroupName
            : $"{GroupName} — {CourseName}";
    }
}
