using System;

namespace Computer_networks.Models
{
    public class TopicNote
    {
        public int NoteID { get; set; }
        public int UserID { get; set; }
        public int TopicID { get; set; }
        public string NoteText { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Для отображения (заполняется из Join)
        public string TopicTitle { get; set; }
        public string FormattedDate => UpdatedAt?.ToString("dd.MM.yyyy HH:mm") ?? CreatedAt.ToString("dd.MM.yyyy HH:mm");
        public string ShortPreview => NoteText?.Length > 100 ? NoteText.Substring(0, 100) + "…" : NoteText;
    }
}