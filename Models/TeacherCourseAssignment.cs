using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Computer_networks.Models
{
    /// <summary>
    /// Используется в AdminPanel для отображения списка преподавателей
    /// с флагом — назначен ли каждый на конкретный курс.
    /// </summary>
    public class TeacherCourseAssignment
    {
        public int TeacherID { get; set; }
        public string TeacherName { get; set; }
        public string Email { get; set; }
        public int CourseID { get; set; }
        public string CourseName { get; set; }
        public bool IsAssigned { get; set; }
        public System.DateTime? AssignedAt { get; set; }

        public string StatusText => IsAssigned ? "✅ Назначен" : "—";
        public string AssignedAtText => AssignedAt.HasValue
            ? AssignedAt.Value.ToString("dd.MM.yyyy")
            : "";
    }
}