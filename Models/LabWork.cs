using System;

namespace Computer_networks.Models
{
    public class LabWork
    {
        public int LabWorkID { get; set; }
        public int CourseID { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime? Deadline { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public string FileName { get; set; }
        public string StoredName { get; set; }
        public string FilePath { get; set; }
        public int? FileSizeKB { get; set; }
        public DateTime? FileUploadedAt { get; set; }
    }
}
