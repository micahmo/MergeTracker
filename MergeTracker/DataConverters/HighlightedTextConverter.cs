using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;
using HTMLConverter;

namespace MergeTracker.DataConverters
{
    public class HighlightedTextConverter : IValueConverter
    {
        #region IValueConverter members

        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is { } && parameter is string key)
            {
                value = HtmlToXamlConverter.ConvertHtmlToXaml(Regex.Replace(value.ToString(), $"({TextData.GetTextData(key)})", "<b>$1</b>"), asFlowDocument: false);
            }

            return value;
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as string)?.RemoveXamlFormatting();
        }

        #endregion

        #region Public static properties

        /// <summary>
        /// Allows the HighlightedTextConverter to determine which text is important, as supplied by the ITextData dependency
        /// </summary>
        public static ITextData TextData { get; set; }

        #endregion
    }
}