using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using LiteDB;

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
                case nameof(RootConfiguration.BugNumberFilter):
                case nameof(RootConfiguration.ChangesetNumberFilter):
                case nameof(RootConfiguration.TargetBranchFilter):
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

            MergeTarget mergeTarget = new MergeTarget {IsOriginal = true};
            DatabaseEngine.MergeTargetCollection.Insert(mergeTarget);

            mergeItem.MergeTargets.Add(mergeTarget);

            mergeItem.Save();

            ReloadMergeItems();
        }

        private void ReloadMergeItems()
        {
            Mouse.OverrideCursor = Cursors.Wait;

            // Important: Always save the RootConfiguration first.
            // 1. This allows us to query on the latest data.
            // 2. This prevents us from losing any data when we clear the MergeItem list from memory.
            Model.RootConfiguration.Save();

            ILiteCollection<MergeItem> mergeItemsCollection = DatabaseEngine.MergeItemCollection.Include(i => i.MergeTargets);
            IEnumerable<MergeItem> mergeItems = null;
            ILiteQueryable<MergeTarget> mergeTargetsQuery = null;

            if (string.IsNullOrEmpty(Model.RootConfiguration.BugNumberFilter) == false)
            {
                mergeTargetsQuery = (mergeTargetsQuery ?? DatabaseEngine.MergeTargetCollection.Query())
                    .Where(t => t.BugNumber.Contains(Model.RootConfiguration.BugNumberFilter));
            }

            if (string.IsNullOrEmpty(Model.RootConfiguration.ChangesetNumberFilter) == false)
            {
                mergeTargetsQuery = (mergeTargetsQuery ?? DatabaseEngine.MergeTargetCollection.Query())
                    .Where(t => t.Changeset.Contains(Model.RootConfiguration.ChangesetNumberFilter));
            }

            if (string.IsNullOrEmpty(Model.RootConfiguration.TargetBranchFilter) == false)
            {
                mergeTargetsQuery = (mergeTargetsQuery ?? DatabaseEngine.MergeTargetCollection.Query())
                    .Where(t => t.TargetBranch.Contains(Model.RootConfiguration.TargetBranchFilter));
            }

            if (Model.RootConfiguration.NotCompletedFilter)
            {
                mergeTargetsQuery = (mergeTargetsQuery ?? DatabaseEngine.MergeTargetCollection.Query())
                    .Where(t => t.IsCompleted == false);
            }
            
            // We have finished filtering. See if we have any query on MergeTargets, in which case we need to apply this filter to the MergeItems.
            if (mergeTargetsQuery is { })
            {
                string queryString = string.Empty;
                foreach (int matchingMergeTargetId in mergeTargetsQuery.Select(t => t.ObjectId).ToEnumerable())
                {
                    if (string.IsNullOrEmpty(queryString) == false)
                    {
                        queryString += "OR ";
                    }

                    queryString += $"$.MergeTargets[*].$id ANY = {matchingMergeTargetId}";
                }

                if (string.IsNullOrEmpty(queryString) == false)
                {
                    mergeItems = mergeItemsCollection.Find(queryString);
                }
                else
                {
                    mergeItems = Enumerable.Empty<MergeItem>();
                }
            }

            Model.RootConfiguration.MergeItems.Clear();
            (mergeItems ?? mergeItemsCollection.FindAll()).OrderByDescending(i => i.ObjectId).ToList().ForEach(i => Model.RootConfiguration.MergeItems.Add(i));

            Mouse.OverrideCursor = null;
        }

        private void ClearFilters()
        {
            Model.RootConfiguration.BugNumberFilter = string.Empty;
            Model.RootConfiguration.ChangesetNumberFilter = string.Empty;
            Model.RootConfiguration.TargetBranchFilter = string.Empty;
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
