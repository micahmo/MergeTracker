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

        /// <summary>
        /// Adds a clause to a query string. If the query is empty or null, the given <paramref name="clause"/> is returned.
        /// Otherwise, the given <paramref name="query"/> is returned, suffixed by the <paramref name="operator"/> and <paramref name="clause"/>.
        /// Example:
        ///   <code>Given query = "", operator = "OR", clause = "ID = 5", returns "ID = 5"</code>
        ///   <code>Given query = "Name = 'test'", operator = "OR", clause = "ID = 5", returns "Name = 'test' OR ID = 5"</code>
        /// </summary>
        public static string AddClause(this string query, string @operator, string clause)
        {
            if (string.IsNullOrEmpty(query) == false)
            {
                query = $"{query} {@operator} ";
            }

            query = $"{query}{clause}";

            return query;
        }

        #endregion
    }
}
