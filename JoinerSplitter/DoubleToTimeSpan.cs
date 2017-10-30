namespace JoinerSplitter
{
    using System;
    using System.Globalization;
    using System.Windows.Data;

    public class DoubleToTimeSpan : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var val = (double)(value ?? throw new ArgumentNullException(nameof(value)));
            return TimeSpan.FromSeconds(val);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var val = (TimeSpan)(value ?? throw new ArgumentNullException(nameof(value)));
            return val.TotalSeconds;
        }
    }
}