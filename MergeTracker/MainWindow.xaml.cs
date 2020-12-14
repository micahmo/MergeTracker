using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using LiteDB;
using MergeTracker.DataConverters;
using Xctk = Xceed.Wpf.Toolkit;

namespace MergeTracker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = Model;

            try
            {
                Model.RootConfiguration = RootConfiguration.Load();
            }
            catch (IOException)
            {
                MessageBox.Show("There was an error accessing the database configuration file. Shutting down.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }

            Model.RootConfiguration.PropertyChanged += RootConfiguration_PropertyChanged;

            Model.Commands.ReloadMergeItemsCommand.Execute(null);

            MergeItem.MergeItemDeleted += MergeItem_MergeItemDeleted;

            // Set the HighlightedTextConverter's dependency so that it can retrieve the important text from the current configuration
            HighlightedTextConverter.TextData = Model.RootConfiguration;

            Timer autoSaveTimer = new Timer { Interval = TimeSpan.FromMinutes(1).TotalMilliseconds };
            autoSaveTimer.Elapsed += AutoSaveTimer_Elapsed;
            autoSaveTimer.Start();
        }

        private void MergeItem_MergeItemDeleted(object sender, EventArgs e)
        {
            Model.Commands.ReloadMergeItemsCommand.Execute(null);
        }

        private void AutoSaveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            RootConfiguration.Instance.Save();
        }

        private void RootConfiguration_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(RootConfiguration.Filter):
                case nameof(RootConfiguration.NotCompletedFilter):
                    Model.Commands.ReloadMergeItemsCommand.Execute(null);
                    break;
            }
        }

        private MainWindowModel Model { get; } = new MainWindowModel();

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            RootConfiguration.Instance.Save();
            DatabaseEngine.Shutdown();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            Model.RootConfiguration.TfsPassword = TfsPasswordBox.Password;
        }

        private void ReloadCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Model.Commands.ReloadMergeItemsCommand.Execute(null);
        }

        // Fired when the user presses a key in one of the "item" TextBoxes, such as Work Item or Changeset
        // Use PreviewKeyDown because some keys get filtered out due to funky logic in the base OnKeyDown handler.
        private async void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Control-Enter will allow the user to quickly open an item
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Find our sender DataGrid, with its bound MergeItem
                // and our sender RichTextBox with its bound MergeTarget
                if (sender is DataGrid dataGrid && dataGrid.DataContext is MergeItem mergeItem &&
                    e.OriginalSource is Xctk.RichTextBox textBox && textBox.DataContext is MergeTarget mergeTarget)
                {
                    // Now find out which one
                    var bindingExpression = textBox.GetBindingExpression(Xctk.RichTextBox.TextProperty);
                    string bindingPath = bindingExpression?.ParentBinding.Path.Path;
                    
                    switch (bindingPath)
                    {
                        case nameof(MergeTarget.BugNumber):
                            if (int.TryParse(mergeTarget.BugNumber, out int bugNumber))
                            {
                                await mergeItem.OpenBug(mergeTarget.WorkItemServer, bugNumber);
                            }
                            break;
                        case nameof(mergeTarget.Changeset):
                            if (string.IsNullOrEmpty(mergeTarget.Changeset) == false)
                            {
                                await mergeItem.OpenChangeset(mergeTarget.SourceControlServer, mergeTarget.Changeset);
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
        }
    }

    internal class MainWindowModel : ObservableObject
    {
        public MainWindowModel()
        {
            Commands = new MainWindowCommands(this);
        }

        public MainWindowCommands Commands { get; }

        public RootConfiguration RootConfiguration
        {
            get => _rootConfiguration;
            set => Set(nameof(RootConfiguration), ref _rootConfiguration, value);
        }
        private RootConfiguration _rootConfiguration;
    }

    internal class MainWindowCommands
    {
        public MainWindowCommands(MainWindowModel mainWindowModel) => Model = mainWindowModel;

        private MainWindowModel Model { get; }

        public ICommand CreateMergeItemCommand => _createMergeItemCommand ??= new RelayCommand(CreateMergeItem);
        private RelayCommand _createMergeItemCommand;

        public ICommand ReloadMergeItemsCommand => _reloadMergeItemsCommand ??= new RelayCommand(ReloadMergeItems);
        private RelayCommand _reloadMergeItemsCommand;

        public ICommand ClearFiltersCommand => _clearFiltersCommand ??= new RelayCommand(ClearFilters);
        private RelayCommand _clearFiltersCommand;

        public ICommand ShowMergeTargetContextMenuCommand => _showMergeItemContextMenuCommand ??= new RelayCommand<DataGridRow>(ShowMergeTargetContextMenu);
        private RelayCommand<DataGridRow> _showMergeItemContextMenuCommand;

        public ICommand ToggleTfsSettingsVisibilityCommand => _toggleTfsSettingsVisibilityCommand ??= new RelayCommand(ToggleTfsSettingsVisibility);
        private RelayCommand _toggleTfsSettingsVisibilityCommand;

        private void CreateMergeItem()
        {
            MergeItem mergeItem = new MergeItem {Name = "New Merge Item"};
            DatabaseEngine.MergeItemCollection.Insert(mergeItem);

            MergeTarget mergeTarget = new MergeTarget {IsOriginal = true, IsCompleted = false};
            DatabaseEngine.MergeTargetCollection.Insert(mergeTarget);

            mergeItem.MergeTargets.Add(mergeTarget);

            mergeItem.Save();

            ReloadMergeItems();
        }

        private void ReloadMergeItems()
        {
            // Important: Always save the RootConfiguration first.
            // 1. This allows us to query on the latest data.
            // 2. This prevents us from losing any data when we clear the MergeItem list from memory.
            Model.RootConfiguration.Save();

            #region Notes about implementation

            // TODO: Change these queries to use C# query syntax and PredicateBuilder once issue 1897 is fixed in LiteDB.
            // https://github.com/mbdavid/LiteDB/issues/1897
            // For now, we have to hard-code the query syntax for all parameters.

            // I also could not get parameterization to work without issues, so we are potentially vulnerable to parameter injection.
            // That being said, I was not able to get an injection to work.
            //  1. It looks like LiteDB cannot support multiple statements, so ending the query and staring a new line
            //     (that does anything other than SELECT) doesn't seem to work.
            //  2. We are not directly building the whole statement using interpolated strings, only the were clause.
            //     It seems like, at worst, we'll have a poorly formed where clause, but no injection.

            // Below is the ONLY way I could get everything to work with complex queries. :-)

            #endregion

            var mergeItemsQuery = DatabaseEngine.MergeItemCollection.Include(i => i.MergeTargets);
            string query = null;
            string subQuery = null;

            // NotCompletedFilter has highest precedence, so it must be an AND.
            if (Model.RootConfiguration.NotCompletedFilter)
            {
                query = query.AddClause("AND", "COUNT(FILTER($.MergeTargets[*]=>@.IsCompleted!=true))>0");
            }

            if (string.IsNullOrEmpty(Model.RootConfiguration.Filter) == false)
            {
                subQuery = subQuery.AddClause("AND", $"Name LIKE '%{Model.RootConfiguration.Filter}%'");
            }

            if (string.IsNullOrEmpty(Model.RootConfiguration.Filter) == false)
            {
                subQuery = subQuery.AddClause("OR", "COUNT(FILTER($.MergeTargets[*]=>" +
                                                    $"@.BugNumber LIKE '%{Model.RootConfiguration.Filter}%' OR " +
                                                    $"@.Changeset LIKE '%{Model.RootConfiguration.Filter}%' OR " +
                                                    $"@.TargetBranch LIKE '%{Model.RootConfiguration.Filter}%'" +
                                                    "))>0");
            }

            if (string.IsNullOrEmpty(subQuery) == false)
            {
                query = query.AddClause("AND", $"({subQuery})");
            }

            // Use a wait cursor with a specific dispatcher priority, so that we can ensure that it doesn't change until the UI is responsive
            using (new WaitCursor(Cursors.Wait, DispatcherPriority.Loaded))
            {
                Model.RootConfiguration.MergeItems.Clear();

                try
                {
                    mergeItemsQuery.Find(query ?? "1=1").OrderByDescending(i => i.ObjectId).ToList().ForEach(i => Model.RootConfiguration.MergeItems.Add(i));
                }
                catch (LiteException)
                {
                    // Exceptions would be caused by poorly formed where clauses, so we'll just let it go with no results
                    // (We've already cleared the in-memory list.)
                }
            }
        }

        private void ClearFilters()
        {
            Model.RootConfiguration.Filter = string.Empty;
            Model.RootConfiguration.NotCompletedFilter = false;
        }

        private void ShowMergeTargetContextMenu(DataGridRow row)
        {
            if (row.ContextMenu is { })
            {
                // Important: Must initialize PlacementTarget before showing ContextMenu,
                // otherwise binding will not be able to resolve.
                row.ContextMenu.PlacementTarget = row;

                // Show the context menu
                row.ContextMenu.IsOpen = true;
            }
        }

        private void ToggleTfsSettingsVisibility()
        {
            Model.RootConfiguration.ShowTfsSettings = !Model.RootConfiguration.ShowTfsSettings;
        }
    }
}
