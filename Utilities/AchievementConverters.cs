using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Computer_networks.Utilities
{
    public class UnlockedToBackground : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isUnlocked = (bool)value;
            return isUnlocked ? "#F0FFF0" : "#FFFFFF";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class UnlockedToBorder : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isUnlocked = (bool)value;
            return isUnlocked ? "#28A745" : "#DDDDDD";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class UnlockedToTitleColor : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isUnlocked = (bool)value;
            return isUnlocked ? "#28A745" : "#2C3E50";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class UnlockedToProgressColor : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isUnlocked = (bool)value;
            return isUnlocked ? "#28A745" : "#007ACC";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class PercentToWidth : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int percent = (int)value;
            double maxWidth = double.Parse(parameter.ToString());
            return percent * maxWidth / 100;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}