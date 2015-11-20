using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace JoinerSplitter
{
    public class DoubleTimeSpanToString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var val = (double)value;
            return TimeSpan.FromSeconds(val).ToString("hh\\:mm\\:ss\\.fff");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var val = value.ToString();
            return TimeSpan.Parse(val, CultureInfo.InvariantCulture).TotalSeconds;
        }
    }
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
