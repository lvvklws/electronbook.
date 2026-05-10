using System;
using System.Globalization;
using System.Windows.Data;

namespace Computer_networks.Utilities
{
    public class BoolToSelectedBackground : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSelected)
            {
                return isSelected ? "#E3F2FD" : "White";
            }
            return "White";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}