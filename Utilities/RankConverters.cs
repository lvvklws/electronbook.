using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Computer_networks.Utilities
{
    public class RankToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int rank)
            {
                if (rank == 1)
                    return new SolidColorBrush(Color.FromRgb(255, 215, 0));   // Золото
                if (rank == 2)
                    return new SolidColorBrush(Color.FromRgb(192, 192, 192)); // Серебро
                if (rank == 3)
                    return new SolidColorBrush(Color.FromRgb(205, 127, 50));  // Бронза

                return new SolidColorBrush(Color.FromRgb(0, 122, 204));    // Синий
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RankToMedalVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int rank)
            {
                return rank <= 3 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RankToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isCurrentUser && isCurrentUser)
            {
                return new SolidColorBrush(Color.FromRgb(255, 249, 196)); // #FFF9C4
            }
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RankToBorderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isCurrentUser && isCurrentUser)
            {
                return new SolidColorBrush(Color.FromRgb(255, 193, 7)); // #FFC107
            }
            return new SolidColorBrush(Color.FromRgb(224, 224, 224)); // #E0E0E0
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RankToBorderThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isCurrentUser && isCurrentUser)
            {
                return new Thickness(2);
            }
            return new Thickness(1);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}