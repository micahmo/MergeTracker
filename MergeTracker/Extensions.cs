using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;

namespace MergeTracker
{
    public static class Extensions
    {
        #region String extensions

        /// <summary>
        /// Takes a string with XAML formatting and returns only the text
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        public static string RemoveXamlFormatting(this string document)
        {
            // Convert the formatted string to a FlowDocument
            FlowDocument flowDocument = new FlowDocument();
            flowDocument.Blocks.Add((Section)XamlReader.Parse(document));
            
            // Add FlowDocument to RichTextBox
            RichTextBox rtb = new RichTextBox { Document = flowDocument };
            
            // Use TextRange's Text property to get plaintext string
            string clean = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd).Text?.TrimEnd();
            
            return clean;
        }

        #endregion
    }
}
