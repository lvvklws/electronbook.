using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Computer_networks.Data
{
    public class ColumnWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
      
            if (value is double actualWidth)
            {
                double fixedWidths = 0;
                if (parameter is string paramString && double.TryParse(paramString, out double parsedFixedWidths))
                {
                    fixedWidths = parsedFixedWidths;
                }

                double remainingWidth = actualWidth - fixedWidths - 20;

                return Math.Max(50, remainingWidth);
            }
            return 50; 
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
