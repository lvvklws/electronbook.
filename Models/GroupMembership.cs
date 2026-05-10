using System;

namespace Computer_networks.Models
{
    /// <summary>
    /// Модель связи студента с группой
    /// </summary>
    public class GroupMembership
    {
        public int MembershipID { get; set; }                 // ID записи
        public int GroupID { get; set; }                       // ID группы
        public int UserID { get; set; }                        // ID студента
        public DateTime AddedAt { get; set; }                  // Дата добавления

        // Дополнительные поля для отображения (заполняются через JOIN)
        public string Username { get; set; }                   // Логин студента
        public string Email { get; set; }                      // Email студента
    }
}