using System;
using System.ComponentModel;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

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
            Model.RootConfiguration = RootConfiguration.Load();

            Model.RootConfiguration.PropertyChanged += RootConfiguration_PropertyChanged;

            Model.Commands.FilterCommand.Execute(null);

            Timer autoSaveTimer = new Timer { Interval = TimeSpan.FromMinutes(1).TotalMilliseconds };
            autoSaveTimer.Elapsed += AutoSaveTimer_Elapsed; ;
            autoSaveTimer.Start();
        }

        private void AutoSaveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            RootConfiguration.Save(Model.RootConfiguration);
        }

        private void RootConfiguration_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(RootConfiguration.BugNumberFilter):
                case nameof(RootConfiguration.ChangesetNumberFilter):
                case nameof(RootConfiguration.TargetBranchFilter):
                case nameof(RootConfiguration.NotCompletedFilter):
                    Model.Commands.FilterCommand.Execute(null);
                    break;
            }
        }

        private MainWindowModel Model { get; } = new MainWindowModel();

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            RootConfiguration.Save(Model.RootConfiguration);
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

        public ICommand FilterCommand => _filterCommand ??= _filterCommand = new RelayCommand(Filter);
        private RelayCommand _filterCommand;

        public ICommand ClearFiltersCommand => _clearFiltersCommand ??= new RelayCommand(ClearFilters);
        private RelayCommand _clearFiltersCommand;

        public ICommand ShowMergeItemContextMenuCommand => _showMergeItemContextMenuCommand ??= new RelayCommand<MergeItemGrid>(ShowMergeItemContextMenu);
        private RelayCommand<MergeItemGrid> _showMergeItemContextMenuCommand;

        private void CreateMergeItem()
        {
            Model.RootConfiguration.MergeItems.Insert(0, new MergeItem(createDefaultTarget: true) {Name = "New Merge Item"});
        }

        private void Filter()
        {
            Model.RootConfiguration.MergeItems.ToList().ForEach(i => i.IsFiltered = false);


            if (string.IsNullOrEmpty(Model.RootConfiguration.BugNumberFilter) == false)
            {
                Model.RootConfiguration.MergeItems.Where(i => i.MergeTargets.Any(t => t.BugNumber?.Contains(Model.RootConfiguration.BugNumberFilter) == true) == false)
                    .ToList().ForEach(i => i.IsFiltered = true);
            }

            if (string.IsNullOrEmpty(Model.RootConfiguration.ChangesetNumberFilter) == false)
            {
                Model.RootConfiguration.MergeItems.Where(i => i.MergeTargets.Any(t => t.Changeset?.Contains(Model.RootConfiguration.ChangesetNumberFilter) == true) == false)
                    .ToList().ForEach(i => i.IsFiltered = true);
            }

            if (string.IsNullOrEmpty(Model.RootConfiguration.TargetBranchFilter) == false)
            {
                Model.RootConfiguration.MergeItems.Where(i => i.MergeTargets.Any(t => t.TargetBranch?.Contains(Model.RootConfiguration.TargetBranchFilter) == true) == false)
                    .ToList().ForEach(i => i.IsFiltered = true);
            }

            if (Model.RootConfiguration.NotCompletedFilter)
            {
                Model.RootConfiguration.MergeItems.Where(i => i.MergeTargets.All(t => t.IsCompleted)).ToList().ForEach(i => i.IsFiltered = true);
            }
        }

        private void ClearFilters()
        {
            Model.RootConfiguration.BugNumberFilter = string.Empty;
            Model.RootConfiguration.ChangesetNumberFilter = string.Empty;
            Model.RootConfiguration.TargetBranchFilter = string.Empty;
            Model.RootConfiguration.NotCompletedFilter = false;
        }

        private void ShowMergeItemContextMenu(MergeItemGrid grid)
        {
            if (grid.ContextMenu is { })
            {
                grid.ContextMenu.IsOpen = true;
            }
        }
    }
}
