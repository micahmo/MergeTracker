using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
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
            DataContext = Model ??= new MainWindowModel(this);

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

        private MainWindowModel Model { get; }

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

        private void GoToItemCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // First, check if there is any selected text. We want this to become the seed for the GoTo box

            IInputElement focusedControl = FocusManager.GetFocusedElement(this);
            var selectedTextProperty = focusedControl?.GetType().GetProperty(nameof(TextBox.SelectedText), BindingFlags.Instance | BindingFlags.Public);
            var textValue = selectedTextProperty?.GetValue(focusedControl) as string;

            if (string.IsNullOrEmpty(textValue) == false)
            {
                Model.RootConfiguration.SelectedItemId = textValue;
            }

            Model.Commands.ShowGoToItemCommand.Execute(null);
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
                                await Model.RootConfiguration.OpenBug(mergeTarget.WorkItemServer, bugNumber, mergeItem);
                            }
                            break;
                        case nameof(mergeTarget.Changeset):
                            if (string.IsNullOrEmpty(mergeTarget.Changeset) == false)
                            {
                                await Model.RootConfiguration.OpenChangeset(mergeTarget.SourceControlServer, mergeTarget.Changeset, mergeItem);
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Model.RaisePropertyChanged(nameof(Model.PromptForOpenItemWidth));
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Model.ShowGoToItemPrompt)
            {
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
                {
                    Model.Commands.GoToItemCommand?.Execute(null);
                }
                else if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
                {
                    Model.Commands.CloseGoToItemCommand?.Execute(null);
                }
            }
        }

        // On LostFocus, we need to force the RichTextBox to update with the latest formatting as determined by the converter.
        // The only way this could happen automatically is if we either...
        //  a. Set UpdateSourceTrigger to LostFocus, but then the update would be too slow and we'd miss some changes, or
        //  b. Set a Delay on UpdateSourceTrigger of PropertyChanged, but then it interrupts the user's workflow (puts the cursor back at the beginning)
        // As it stands, on LostFocus, the correct value IS run through the converter and assigned back to the RichTextBox.Text property,
        // but the document in the underlying WPF RichTextBox does not update. Calling "UpdateDocumentFromText" forces it to.
        private void RichTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Xctk.RichTextBox richTextBox)
            {
                richTextBox.GetType().GetMethod("UpdateDocumentFromText", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(richTextBox, null);
            }
        }
    }

    internal class MainWindowModel : ObservableObject
    {
        public MainWindowModel(MainWindow mainWindow)
        {
            Commands = new MainWindowCommands(this);
            _mainWindow = mainWindow;
        }

        public MainWindowCommands Commands { get; }

        private readonly MainWindow _mainWindow;

        public RootConfiguration RootConfiguration
        {
            get => _rootConfiguration;
            set => Set(nameof(RootConfiguration), ref _rootConfiguration, value);
        }
        private RootConfiguration _rootConfiguration;

        public bool ShowGoToItemPrompt
        {
            get => _showGoToItemPrompt;
            set => Set(nameof(ShowGoToItemPrompt), ref _showGoToItemPrompt, value);
        }
        private bool _showGoToItemPrompt;

        public bool GoingToItem
        {
            get => _goingToItem;
            set => Set(nameof(GoingToItem), ref _goingToItem, value);
        }
        private bool _goingToItem;

        public double PromptForOpenItemWidth => Math.Min(_mainWindow.ActualWidth - 100, 600);

        public bool ErrorOpeningItem
        {
            get => _errorOpeningItem;
            set => Set(nameof(ErrorOpeningItem), ref _errorOpeningItem, value);
        }
        private bool _errorOpeningItem;

        public IEnumerable<ItemType> ItemTypes => Enum.GetValues(typeof(ItemType)).OfType<ItemType>();
    }

    internal class MainWindowCommands
    {
        public MainWindowCommands(MainWindowModel mainWindowModel) => Model = mainWindowModel;

        private MainWindowModel Model { get; }

        public ICommand CreateMergeItemCommand => _createMergeItemCommand ??= new RelayCommand(CreateMergeItem);
        private RelayCommand _createMergeItemCommand;

        public ICommand ReloadMergeItemsCommand => _reloadMergeItemsCommand ??= new RelayCommand(ReloadMergeItems);
        private RelayCommand _reloadMergeItemsCommand;

        public ICommand ShowGoToItemCommand => _showGoToItemCommand ??= new RelayCommand(ShowGoToItem);
        private RelayCommand _showGoToItemCommand;

        public ICommand GoToItemCommand => _goToItemCommand ??= new RelayCommand(GoToItem);
        private RelayCommand _goToItemCommand;

        public ICommand CloseGoToItemCommand => _closeGoToItemCommand ??= new RelayCommand(CloseGoToItem);
        private RelayCommand _closeGoToItemCommand;

        public ICommand CopyItemUrlCommand => _copyItemUrlCommand ??= new RelayCommand(CopyItemUrl);
        private RelayCommand _copyItemUrlCommand;

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

        private void ShowGoToItem()
        {
            Model.ShowGoToItemPrompt = true;
            Model.ErrorOpeningItem = false;
        }

        private async void GoToItem()
        {
            if (Model.ShowGoToItemPrompt)
            {
                Model.GoingToItem = true;
                Model.ErrorOpeningItem = false;

                bool success = false;

                switch (Model.RootConfiguration.SelectedItemType)
                {
                    case ItemType.WorkItem:
                        if (int.TryParse(Model.RootConfiguration.SelectedItemId, out int bugNumber))
                        {
                            success = await Model.RootConfiguration.OpenBug(Model.RootConfiguration.SelectedWorkItemServer, bugNumber);
                        }
                        break;
                    case ItemType.Changeset:
                        if (string.IsNullOrEmpty(Model.RootConfiguration.SelectedItemId) == false)
                        {
                            success = await Model.RootConfiguration.OpenChangeset(Model.RootConfiguration.SelectedSourceControlServer, Model.RootConfiguration.SelectedItemId);
                        }
                        break;
                    default:
                        break;
                }

                if (success)
                {
                    Model.ShowGoToItemPrompt = false;
                }
                else
                {
                    Model.ErrorOpeningItem = true;
                }

                Model.GoingToItem = false;
            }
        }

        private void CloseGoToItem()
        {
            Model.ShowGoToItemPrompt = false;
        }

        private async void CopyItemUrl()
        {
            Model.GoingToItem = true;
            Model.ErrorOpeningItem = false;

            bool success = false;

            switch (Model.RootConfiguration.SelectedItemType)
            {
                case ItemType.WorkItem:
                    if (int.TryParse(Model.RootConfiguration.SelectedItemId, out int bugNumber))
                    {
                        success = await Model.RootConfiguration.CopyBugUrl(Model.RootConfiguration.SelectedWorkItemServer, bugNumber);
                    }

                    break;
                case ItemType.Changeset:
                    if (string.IsNullOrEmpty(Model.RootConfiguration.SelectedItemId) == false)
                    {
                        success = await Model.RootConfiguration.CopyChangesetOrCommitUrl(Model.RootConfiguration.SelectedSourceControlServer, Model.RootConfiguration.SelectedItemId);
                    }

                    break;
                default:
                    break;
            }

            if (success)
            {
                Model.ShowGoToItemPrompt = false;
            }
            else
            {
                Model.ErrorOpeningItem = true;
            }

            Model.GoingToItem = false;
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
