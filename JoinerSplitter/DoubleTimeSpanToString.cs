namespace JoinerSplitter
{
    using System;
    using System.Globalization;
    using System.Windows.Data;

    public class DoubleTimeSpanToString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var val = (double)(value ?? throw new ArgumentNullException(nameof(value)));
            return TimeSpan.FromSeconds(val).ToString("hh\\:mm\\:ss\\.fff", CultureInfo.InvariantCulture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var val = value.ToString();
            return TimeSpan.Parse(val, CultureInfo.InvariantCulture).TotalSeconds;
        }
    }
}