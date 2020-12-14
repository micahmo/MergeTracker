using System;
using System.Windows;

namespace MergeTracker
{
    /// <summary>
    /// Interaction logic for GoToItemGrid.xaml
    /// </summary>
    public partial class GoToItemGrid
    {
        public GoToItemGrid()
        {
            InitializeComponent();
        }

        private void Grid_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                HighlightTextBox();
            }
        }

        private void ErrorTextBlock_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (ErrorTextBox.Visibility == Visibility.Visible)
            {
                HighlightTextBox();
            }
        }

        private void HighlightTextBox()
        {
            // We were just shown. Focus and highlight the item id textbox.
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ItemIdTextBox.Focus();
                ItemIdTextBox.SelectAll();
            }));
        }
    }
}
