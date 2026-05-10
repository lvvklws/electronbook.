using System;

namespace Computer_networks.Models
{
    public class GlossaryTerm
    {
        public int TermID { get; set; }        // Уникальный идентификатор
        public string Term { get; set; }        // Термин (HTML, CSS, и т.д.)
        public string Definition { get; set; }  // Определение термина
        public int CourseID { get; set; } = 0; // #6 #7 ИСПРАВЛЕНО: значение устанавливается через SqlDataAccess.CurrentCourseId
        public int? TopicID { get; set; }       // К какой теме относится (необязательно)
        public DateTime CreatedAt { get; set; } // Дата добавления
    }
}