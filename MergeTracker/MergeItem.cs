using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Newtonsoft.Json;

namespace MergeTracker
{
    public class MergeItem : ObservableObject
    {
        public MergeItem()
        {
            Commands = new MergeItemCommands(this);

            MergeTargets.CollectionChanged += MergeTargets_CollectionChanged;
        }

        private void MergeTargets_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            e.OldItems?.OfType<MergeTarget>().ToList().ForEach(t =>
            {
                t.PropertyChanged -= MergeTarget_PropertyChanged;
            });

            e.NewItems?.OfType<MergeTarget>().ToList().ForEach(t =>
            {
                t.PropertyChanged += MergeTarget_PropertyChanged;
            });
        }

        private void MergeTarget_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MergeTarget.BugNumber):
                    GenerateName(sender as MergeTarget);
                    break;
            }
        }

        private async void GenerateName(MergeTarget mergeTarget)
        {
            if (RootConfiguration.Instance.UseTfs &&
                Name == "New Merge Item" && mergeTarget?.IsOriginal == true && int.TryParse(mergeTarget.BugNumber, out int bugNumber))
            {
                Name = (await TfsUtils.GetWorkItem(bugNumber))?.Title ?? Name;
            }
        }

        public MergeItem(bool createDefaultTarget) : this()
        {
            if (createDefaultTarget)
            {
                MergeTargets.Add(new MergeTarget {IsOriginal = true});
            }
        }

        public string Name
        {
            get => _name;
            set => Set(nameof(Name), ref _name, value);
        }
        private string _name;

        [JsonIgnore]
        public bool IsFiltered
        {
            get => _isFiltered;
            set => Set(nameof(IsFiltered), ref _isFiltered, value);
        }
        private bool _isFiltered;

        [JsonIgnore]
        public MergeItemCommands Commands { get; }

        public ObservableCollection<MergeTarget> MergeTargets { get; } = new ObservableCollection<MergeTarget>();
    }

    public class MergeItemCommands
    {
        public MergeItemCommands(MergeItem mergeItem) => Model = mergeItem;

        private MergeItem Model { get; }

        public ICommand CreateMergeTargetCommand => _createMergeTargetCommand ??= new RelayCommand(CreateMergeTarget);
        private RelayCommand _createMergeTargetCommand;

        public ICommand DeleteCommand => _deleteCommand ??= new RelayCommand(Delete);
        private RelayCommand _deleteCommand;

        public ICommand DeleteMergeTargetCommand => _deleteMergeTargetCommand ??= new RelayCommand<DataGrid>(DeleteMergeTarget);
        private RelayCommand<DataGrid> _deleteMergeTargetCommand;

        public ICommand CopyBugNumberCommand => _copyBugNumberCommand ??= new RelayCommand<DataGrid>(CopyBugNumber);
        private RelayCommand<DataGrid> _copyBugNumberCommand;

        public ICommand OpenBugCommand => _openBugCommand ??= new RelayCommand<DataGrid>(OpenBug);
        private RelayCommand<DataGrid> _openBugCommand;

        public ICommand OpenChangesetCommand => _openChangesetCommand ??= new RelayCommand<DataGrid>(OpenChangeset);
        private RelayCommand<DataGrid> _openChangesetCommand;

        public ICommand GenerateMessageCommand => _generateMessageCommand ??= new RelayCommand<DataGrid>(GenerateMessage);
        private RelayCommand<DataGrid> _generateMessageCommand;

        private void CreateMergeTarget()
        {
            Model.MergeTargets.Add(new MergeTarget());
        }

        private void DeleteMergeTarget(DataGrid dataGrid)
        {
            if (dataGrid.SelectedItem is MergeTarget mergeTarget)
            {
                Model.MergeTargets.Remove(mergeTarget);
            }
        }

        private void Delete()
        {
            MessageBoxResult res = MessageBox.Show("Are you sure you want to delete the merge item?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                RootConfiguration.Instance.MergeItems.Remove(Model);
            }
        }

        private void CopyBugNumber(DataGrid dataGrid)
        {
            if (dataGrid.SelectedItem is MergeTarget mergeTarget && string.IsNullOrEmpty(mergeTarget.BugNumber) == false)
            {
                Clipboard.SetData(DataFormats.Text, mergeTarget.BugNumber);
            }
        }

        private void OpenBug(DataGrid dataGrid)
        {
            if (dataGrid.SelectedItem is MergeTarget mergeTarget && string.IsNullOrEmpty(mergeTarget.BugNumber) == false)
            {
                Process.Start($"http://bel1tfs04.go.johnsoncontrols.com:8080/tfs/TSS/Unified/_workitems?id={mergeTarget.BugNumber}&_a=edit");
            }
        }

        private void OpenChangeset(DataGrid dataGrid)
        {
            if (dataGrid.SelectedItem is MergeTarget mergeTarget && string.IsNullOrEmpty(mergeTarget.Changeset) == false)
            {
                foreach (string changeset in mergeTarget.Changeset.Split(new[] {",", ";"}, StringSplitOptions.RemoveEmptyEntries))
                {
                    Process.Start($"http://bel1tfs04.go.johnsoncontrols.com:8080/tfs/TSS/Unified/_versionControl/changeset/{changeset}");
                }
            }
        }

        private void GenerateMessage(DataGrid dataGrid)
        {
            if (dataGrid.SelectedItem is MergeTarget mergeTarget)
            {
                mergeTarget.GenerateCheckinNote(Model);
            }
        }
    }
}
