namespace Computer_networks.Models
{
    public class UserSetting
    {
        public int SettingID { get; set; }
        public int UserID { get; set; }
        public string FontSize { get; set; } = "Medium";
        public string Theme { get; set; } = "Light";
    }
}