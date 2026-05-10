using System;

namespace Computer_networks.Models
{
    public class LabReportSubmission
    {
        public int SubmissionID { get; set; }
        public int LabWorkID { get; set; }
        public int UserID { get; set; }
        public string FileName { get; set; }
        public string StoredName { get; set; }
        public string FilePath { get; set; }
        public int FileSizeKB { get; set; }
        public DateTime UploadedAt { get; set; }
        public string Comment { get; set; }
        public string Username { get; set; }
        public string LabTitle { get; set; }
    }
}
