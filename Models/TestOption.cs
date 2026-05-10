using System;

namespace Computer_networks.Models
{
    public class TestOption
    {
        public int Id { get; set; }


        public string Title { get; set; }


        public int? TopicID { get; set; }


        public int QuestionCount { get; set; }

        public override string ToString()
        {
            return Title;
        }

        public bool IsBlocked { get; set; }

        // НОВОЕ СВОЙСТВО: Сообщение о блокировке
        public string BlockMessage { get; set; }

        // Свойство для отображения в ListBox (например, "Тест по теме X (Заблокирован)")
        public string DisplayTitle
        {
            get
            {
                if (IsBlocked)
                {
                    return $"{Title} (🚫 Заблокирован)";
                }
                return $"{Title}";
            }
        }
    }
}