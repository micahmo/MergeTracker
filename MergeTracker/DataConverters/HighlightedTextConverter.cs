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
            // Note: Because we're bound to a RichTextBox, we need to have the XAML formatting (even if the string is empty).
            // Therefore, the only time we don't want go go through the formatting branch is if value is null.
            // Do not add any other top-level conditions.

            if (value is { })
            {
                if (TextData.GetTextData(parameter?.ToString()) is { } match && string.IsNullOrEmpty(match) == false)
                {
                    value = HtmlToXamlConverter.ConvertHtmlToXaml(Regex.Replace(value.ToString(), $"({TextData.GetTextData(parameter?.ToString())})", "<b>$1</b>", RegexOptions.IgnoreCase), asFlowDocument: false);
                }
                else
                {
                    value = HtmlToXamlConverter.ConvertHtmlToXaml(value.ToString(), asFlowDocument: false);
                }
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