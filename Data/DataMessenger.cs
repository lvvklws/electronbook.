using System;

namespace Computer_networks.Data
{
    public static class DataMessenger
    {
     
        public static event Action DataChanged;

        public static void NotifyDataChanged()
        {
            DataChanged?.Invoke();
        }
    }
}