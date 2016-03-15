using System;
using System.Globalization;
using System.Windows.Data;

namespace JoinerSplitter
{
    public class DoubleToTimeSpan : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var val = (double)value;
            return TimeSpan.FromSeconds(val);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var val = (TimeSpan)value;
            return val.TotalSeconds;
        }
    }
}