using System;

namespace Computer_networks.Models
{
    public class CodeExample
    {
        public int ExampleID { get; set; }
        public int TopicID { get; set; }
        public string Title { get; set; }
        public string HTMLCode { get; set; }
        public string CSSCode { get; set; }
        public string JSCode { get; set; }
        public int CourseID { get; set; } = 2;
        public DateTime CreatedAt { get; set; }
    }
}