using System;

namespace Computer_networks.Models
{
    public class TopicAttachment
    {
        public int AttachmentID { get; set; }
        public int TopicID { get; set; }
        public string FileName { get; set; }
        public string StoredName { get; set; }
        public string FileType { get; set; }
        public int FileSizeKB { get; set; }
        public DateTime UploadedAt { get; set; }

        public string Icon => FileType switch
        {
            "pdf" => "📄",
            "image" => "🖼️",
            "video" => "🎬",
            _ => "📎"
        };

        public string SizeText => FileSizeKB < 1024
            ? $"{FileSizeKB} КБ"
            : $"{FileSizeKB / 1024.0:F1} МБ";

        public string FullPath => System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Attachments", StoredName);
    }
}