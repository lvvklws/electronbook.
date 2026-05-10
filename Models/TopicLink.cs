using System;

namespace Computer_networks.Models
{
    public class TopicLink
    {
        public int LinkID { get; set; }
        public int TopicID { get; set; }
        public string Title { get; set; }
        public string URL { get; set; }
        public string Description { get; set; }
        public DateTime AddedAt { get; set; }

        public string Domain
        {
            get
            {
                try { return new Uri(URL).Host; }
                catch { return URL; }
            }
        }
    }
}