using System;
using System.Globalization;
using System.Windows.Data;
using Humanizer;

namespace MergeTracker
{
    public class EnumConverter : IValueConverter
    {
        #region IValueConverter members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString().Humanize(LetterCasing.Title);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString().DehumanizeTo(targetType);
        }

        #endregion
    }
}
