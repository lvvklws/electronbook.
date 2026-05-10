using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Computer_networks.Models
{
    public class Bookmark
    {
        public int BookmarkID { get; set; }
        public int UserID { get; set; }
        public int TopicID { get; set; }
        public DateTime DateAdded { get; set; }

        // Полезно для отображения в списке закладок, хотя это не столбец БД
        public string TopicTitle { get; set; }
    }
}
