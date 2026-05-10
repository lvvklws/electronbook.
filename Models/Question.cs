using System.Collections.Generic;

namespace Computer_networks.Models
{
    // Модель для хранения вопроса и всех его вариантов ответа
    public class Question
    {
        public int QuestionID { get; set; }
        public int TopicID { get; set; }
        public string Text { get; set; }

        // #7 ИСПРАВЛЕНО: CourseID не имеет хардкоженного дефолта.
        // Значение устанавливается явно при создании вопроса через SqlDataAccess.CurrentCourseId.
        public int CourseID { get; set; }
        public string QuestionType { get; set; } = "single";
        public int Difficulty { get; set; } = 3;
        public string Explanation { get; set; }

        public List<Answer> Answers { get; set; } = new List<Answer>();
    }

    public class Answer
    {
        public int AnswerID { get; set; }
        public int QuestionID { get; set; }
        public string Text { get; set; } // Текст варианта ответа
        public bool IsCorrect { get; set; } // Правильный ли это ответ
    }
}