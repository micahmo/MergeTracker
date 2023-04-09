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
using Bluegrams.Application;
using Bluegrams.Application.WPF;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using LinqKit;
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

            _updateChecker = new MyUpdateChecker("https://raw.githubusercontent.com/micahmo/MergeTracker/master/MergeTracker/VersionInfo.xml")
            {
                Owner = this,
                DownloadIdentifier = "portable"
            };
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

        private void OnPremTfsPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            Model.RootConfiguration.OnPremTfsPassword = OnPremTfsPasswordBox.Password;
        }

        private void ReloadCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Model.Commands.ReloadMergeItemsCommand.Execute(null);
        }

        private void GoToItemCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // First, check if there is any selected text. We want this to become the seed for the GoTo box

            // Try regular TextBox
            IInputElement focusedControl = FocusManager.GetFocusedElement(this);
            PropertyInfo selectedTextProperty = focusedControl?.GetType().GetProperty(nameof(TextBox.SelectedText), BindingFlags.Instance | BindingFlags.Public);
            string textValue = selectedTextProperty?.GetValue(focusedControl) as string;

            // Try RichTextBox
            if (focusedControl is Xctk.RichTextBox richTextBox)
            {
                textValue = richTextBox.Selection.Text;
            }

            // See if this control has a parent MergeTarget
            if (focusedControl is FrameworkElement { DataContext: MergeTarget mergeTarget } frameworkElement)
            {
                // Figure out of this is a WorkItem or Changeset
                if (frameworkElement.Tag?.ToString() == nameof(MergeTarget.WorkItemId))
                {
                    Model.RootConfiguration.SelectedWorkItemServer = mergeTarget.WorkItemServer;
                    Model.RootConfiguration.SelectedItemType = ItemType.WorkItem;
                }
                else if (frameworkElement.Tag?.ToString() == nameof(MergeTarget.ChangesetId))
                {
                    Model.RootConfiguration.SelectedSourceControlServer = mergeTarget.SourceControlServer;
                    Model.RootConfiguration.SelectedItemType = ItemType.Changeset;
                }
            }

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
                        case nameof(MergeTarget.WorkItemId):
                            await Model.RootConfiguration.OpenWorkItem(mergeTarget.WorkItemServer, mergeTarget.WorkItemId, mergeItem);
                            break;
                        case nameof(mergeTarget.ChangesetId):
                            if (string.IsNullOrEmpty(mergeTarget.ChangesetId) == false)
                            {
                                await Model.RootConfiguration.OpenChangeset(mergeTarget.SourceControlServer, mergeTarget.ChangesetId, mergeItem);
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
            else if (FilterTextBox.IsFocused)
            {
                if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
                {
                    FilterTextBox.Focus();
                    FilterTextBox.Clear();
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

        private void FindCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            FilterTextBox.SelectAll();
            FilterTextBox.Focus();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _updateChecker.CheckForUpdates();
        }

        private void AboutBoxCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            new AboutBox(Icon, showLanguageSelection: false)
            {
                Owner = this,
                UpdateChecker = _updateChecker,
                
            }.ShowDialog();
        }

        public void DefaultWorkItemServerItem_Clicked(object sender, EventArgs e)
        {
            RootConfiguration.Instance.DefaultWorkItemServer = ((sender as MenuItem).DataContext as ServerItem).ServerName;
        }

        public void DefaultSourceControlServerItem_Clicked(object sender, EventArgs e)
        {
            RootConfiguration.Instance.DefaultSourceControlServer = ((sender as MenuItem).DataContext as ServerItem).ServerName;
        }

        #region Private fields

        private readonly WpfUpdateChecker _updateChecker;

        #endregion
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

        public string ErrorOpeningItemToolTip
        {
            get => _errorOpeningItemToolTip;
            set => Set(nameof(ErrorOpeningItemToolTip), ref _errorOpeningItemToolTip, value);
        }
        private string _errorOpeningItemToolTip;

        public IEnumerable<ItemType> ItemTypes => Enum.GetValues(typeof(ItemType)).OfType<ItemType>();
    }

    internal class MainWindowCommands
    {
        public MainWindowCommands(MainWindowModel mainWindowModel) => Model = mainWindowModel;

        private MainWindowModel Model { get; }

        public ICommand CreateMergeItemCommand => _createMergeItemCommand ??= new RelayCommand(CreateMergeItem);
        private RelayCommand _createMergeItemCommand;

        public ICommand ShowDefaultServersMenuCommand => _showDefaultServersMenuCommand ??= new RelayCommand<Button>(ShowDefaultServersMenu);
        private RelayCommand<Button> _showDefaultServersMenuCommand;

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

        public ICommand ToggleProjectSettingsVisibilityCommand => _toggleProjectSettingsVisibilityCommand ??= new RelayCommand(ToggleProjectSettingsVisibility);
        private RelayCommand _toggleProjectSettingsVisibilityCommand;

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

        private void ShowDefaultServersMenu(Button button)
        {
            if (button.ContextMenu != null)
            {
                // Important: Must initialize PlacementTarget before showing ContextMenu,
                // otherwise binding will not be able to resolve.
                button.ContextMenu.PlacementTarget = button;

                button.ContextMenu.IsOpen = true;
            }
        }

        private void ReloadMergeItems()
        {
            // Important: Always save the RootConfiguration first.
            // 1. This allows us to query on the latest data.
            // 2. This prevents us from losing any data when we clear the MergeItem list from memory.
            Model.RootConfiguration.Save();

            #region Notes about implementation

            // Despite https://github.com/mbdavid/LiteDB/issues/1897 not being fixed yet, a solution was suggested to me.
            // The key is to Select the property on which to filter. For example, instead of using:
            //  i.MergeTargets.Where(t => t.IsCompleted == true).Any())
            // use
            //  i.MergeTargets.Select(t => t.IsCompleted).Any(c => c == true)
            // And note that one big Any() is NOT supported, like:
            //  i.MergeTargets.Any(t => t.IsCompleted == true)
            // This seems to solve my issue, so I am able to use ILiteQueryable and PredicateBuilder.

            // Even using this syntax, which generates query parameters, injections are not prevented.
            // That being said, the limitations of LiteDB seem to prevent and real danger from an injection.
            // The most important thing is to handle exceptions upon performing the query, so that malformed queries don't crash.
            //  1. It looks like LiteDB cannot support multiple statements, so ending the query and staring a new line
            //     (that does anything other than SELECT) doesn't seem to work.
            //  2. We are not directly building the whole statement using interpolated strings, only the were clause.
            //     It seems like, at worst, we'll have a poorly formed where clause, but no injection.

            #endregion

            ILiteCollection<MergeItem> mergeItems = DatabaseEngine.MergeItemCollection.Include(i => i.MergeTargets);
            
            // Default the predicate to true, so if there are no query parameters, we get everything.
            ExpressionStarter<MergeItem> queryPredicate = PredicateBuilder.New<MergeItem>(true);
            ExpressionStarter<MergeItem> subQueryPredicate = PredicateBuilder.New<MergeItem>(true);

            // NotCompletedFilter has highest precedence, so it must be an AND.
            if (Model.RootConfiguration.NotCompletedFilter)
            {
                // Use != true so that we get false and null (for indeterminate state).
                queryPredicate = queryPredicate.And(i => i.MergeTargets.Select(t => t.IsCompleted).Any(c => c != true));
            }

            if (string.IsNullOrEmpty(Model.RootConfiguration.Filter) == false)
            {
                // Note: Due to this expression eventually being converted to a DB query with LIKE %%, it is naturally case-insensitive.
                subQueryPredicate = subQueryPredicate.And(i => i.Name.Contains(Model.RootConfiguration.Filter));
            }

            if (string.IsNullOrEmpty(Model.RootConfiguration.Filter) == false)
            {
                subQueryPredicate = subQueryPredicate.Or(i =>
                    i.MergeTargets.Select(t => t.WorkItemId).Any(bn => bn.Contains(Model.RootConfiguration.Filter)) ||
                    i.MergeTargets.Select(t => t.ChangesetId).Any(cs => cs.Contains(Model.RootConfiguration.Filter)) ||
                    i.MergeTargets.Select(t => t.TargetBranch).Any(tb => tb.Contains(Model.RootConfiguration.Filter)) ||
                    i.MergeTargets.Select(t => t.Notes).Any(notes => notes.Contains(Model.RootConfiguration.Filter)));
            }

            // Use a wait cursor with a specific dispatcher priority, so that we can ensure that it doesn't change until the UI is responsive
            using (new WaitCursor(Cursors.Wait, DispatcherPriority.Loaded))
            {
                Model.RootConfiguration.MergeItems.Clear();
                Model.RootConfiguration.MergeItemsTotalCount = 0;

                try
                {
                    mergeItems.Query().Where(queryPredicate.And(subQueryPredicate)).OrderByDescending(i => i.ObjectId).ToList().ForEach(i => Model.RootConfiguration.MergeItems.Add(i));
                    Model.RootConfiguration.MergeItemsTotalCount = mergeItems.Count();
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
            Model.ErrorOpeningItemToolTip = default;
        }

        private async void GoToItem()
        {
            if (Model.ShowGoToItemPrompt)
            {
                Model.GoingToItem = true;
                Model.ErrorOpeningItem = false;
                Model.ErrorOpeningItemToolTip = default;

                string error = default;

                switch (Model.RootConfiguration.SelectedItemType)
                {
                    case ItemType.WorkItem:
                        error = await Model.RootConfiguration.OpenWorkItem(Model.RootConfiguration.SelectedWorkItemServer, Model.RootConfiguration.SelectedItemId);
                        break;
                    case ItemType.Changeset:
                        if (string.IsNullOrEmpty(Model.RootConfiguration.SelectedItemId) == false)
                        {
                            error = await Model.RootConfiguration.OpenChangeset(Model.RootConfiguration.SelectedSourceControlServer, Model.RootConfiguration.SelectedItemId);
                        }
                        break;
                    default:
                        break;
                }

                if (string.IsNullOrEmpty(error))
                {
                    Model.ShowGoToItemPrompt = false;
                }
                else
                {
                    Model.ErrorOpeningItem = true;
                    Model.ErrorOpeningItemToolTip = error;
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
            Model.ErrorOpeningItemToolTip = default;

            string error = default;

            switch (Model.RootConfiguration.SelectedItemType)
            {
                case ItemType.WorkItem:
                    error = await Model.RootConfiguration.CopyWorkItemUrl(Model.RootConfiguration.SelectedWorkItemServer, Model.RootConfiguration.SelectedItemId);
                    break;
                case ItemType.Changeset:
                    if (string.IsNullOrEmpty(Model.RootConfiguration.SelectedItemId) == false)
                    {
                        error = await Model.RootConfiguration.CopyChangesetUrl(Model.RootConfiguration.SelectedSourceControlServer, Model.RootConfiguration.SelectedItemId);
                    }

                    break;
                default:
                    break;
            }

            if (string.IsNullOrEmpty(error))
            {
                Model.ShowGoToItemPrompt = false;
            }
            else
            {
                Model.ErrorOpeningItem = true;
                Model.ErrorOpeningItemToolTip = error;
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

        private void ToggleProjectSettingsVisibility()
        {
            Model.RootConfiguration.ShowProjectSettings = !Model.RootConfiguration.ShowProjectSettings;
        }
    }
}
