using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Computer_networks.Models
{
    public class User
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Email { get; set; }
        public DateTime RegistrationDate { get; set; }
        public int RoleID { get; set; }

        // ── Верификация email ───────────────────────────────────────
        public bool IsEmailVerified { get; set; }

        // ── Проверки роли ──────────────────────────────────────────
        public bool IsAdmin => RoleID == 1;
        public bool IsTeacher => RoleID == 2;
        public bool IsStudent => RoleID == 3;

        // ── Права доступа ──────────────────────────────────────────
        public bool CanManageContent => RoleID == 1 || RoleID == 2;
        public bool CanManageUsers => RoleID == 1;

        // ── Название роли ──────────────────────────────────────────
        public string RoleName
        {
            get
            {
                if (RoleID == 1) return "Администратор";
                if (RoleID == 2) return "Преподаватель";
                if (RoleID == 3) return "Студент";
                return "Неизвестно";
            }
        }

        // ── Цвет бейджа роли (для UI) ──────────────────────────────
        public string RoleBadgeColor
        {
            get
            {
                if (RoleID == 1) return "#CC3333";  // красный  — Администратор
                if (RoleID == 2) return "#007ACC";  // синий    — Преподаватель
                return "#33AA33";                    // зелёный  — Студент
            }
        }

        public ObservableCollection<TestStatistic> TestStatistics { get; set; }
            = new ObservableCollection<TestStatistic>();
    }
}