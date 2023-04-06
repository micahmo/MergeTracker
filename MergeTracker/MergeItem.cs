using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using LiteDB;

namespace MergeTracker
{
    public class MergeItem : ObservableObject
    {
        public MergeItem()
        {
            Commands = new MergeItemCommands(this);

            PropertyChanged += MergeItem_PropertyChanged;
            MergeTargets.CollectionChanged += MergeTargets_CollectionChanged;
        }

        private void MergeItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MergeTargets):
                    MergeTargets.CollectionChanged += MergeTargets_CollectionChanged;
                    MergeTargets.ToList().ForEach(t =>
                    {
                        t.PropertyChanged += MergeTarget_PropertyChanged;
                    });
                    break;
            }
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
                case nameof(MergeTarget.IsOriginal):
                    SetOriginal(sender as MergeTarget);
                    break;
            }
        }

        internal async Task GenerateName(MergeTarget mergeTarget)
        {
            try
            {
                if (mergeTarget?.IsOriginal == true)
                {
                    Name = await RootConfiguration.Instance.GetWorkItemServer(mergeTarget.WorkItemServer).GetWorkItemTitle(mergeTarget.WorkItemId) ?? Name;
                }
            }
            catch (Exception ex)
            {
                LastError = $"There was an error retrieving the work item name.\n\n{ex}";
            }
        }

        private void SetOriginal(MergeTarget changedMergeTarget)
        {
            if (changedMergeTarget.IsOriginal)
            {
                MergeTargets.Where(t => t != changedMergeTarget && t.IsOriginal).ToList().ForEach(t => t.IsOriginal = false);
            }
        }

        [BsonId]
        public int ObjectId { get; set; }

        public string Name
        {
            get => _name;
            set => Set(nameof(Name), ref _name, value);
        }
        private string _name;

        [BsonIgnore]
        public MergeItemCommands Commands { get; }

        [BsonRef("mergetarget")]
        public ObservableCollection<MergeTarget> MergeTargets
        {
            get => _mergeTargets;
            set => Set(nameof(MergeTargets), ref _mergeTargets, value);
        }
        private ObservableCollection<MergeTarget> _mergeTargets = new ObservableCollection<MergeTarget>();

        [BsonIgnore]
        public string LastError
        {
            get => _lastError;
            set
            {
                Set(nameof(LastError), ref _lastError, value);
                RaisePropertyChanged(nameof(HasLastError));
            }
        }
        private string _lastError;

        [BsonIgnore]
        public bool HasLastError => string.IsNullOrEmpty(LastError) == false;

        public static event EventHandler MergeItemDeleted;

        internal static void OnMergeItemDeleted()
        {
            MergeItemDeleted?.Invoke(null, EventArgs.Empty);
        }

        public void Save()
        {
            MergeTargets.ToList().ForEach(t => t.Save());
            DatabaseEngine.MergeItemCollection.Update(this);
        }
    }

    public class MergeItemCommands
    {
        public MergeItemCommands(MergeItem mergeItem) => Model = mergeItem;

        private MergeItem Model { get; }

        public ICommand CreateMergeTargetCommand => _createMergeTargetCommand ??= new RelayCommand(CreateMergeTarget);
        private RelayCommand _createMergeTargetCommand;

        public ICommand DeleteCommand => _deleteCommand ??= new RelayCommand(Delete);
        private RelayCommand _deleteCommand;

        public ICommand PopulateWorkItemNameCommand => _populateWorkItemCommand ??= new RelayCommand<DataGrid>(PopulateWorkItemName);
        private RelayCommand<DataGrid> _populateWorkItemCommand;

        public ICommand DeleteMergeTargetCommand => _deleteMergeTargetCommand ??= new RelayCommand<DataGrid>(DeleteMergeTarget);
        private RelayCommand<DataGrid> _deleteMergeTargetCommand;

        public ICommand CopyWorkItemIdCommand => _copyWorkItemIdCommand ??= new RelayCommand<DataGrid>(CopyWorkItemId);
        private RelayCommand<DataGrid> _copyWorkItemIdCommand;

        public ICommand OpenWorkItemCommand => _openWorkItemCommand ??= new RelayCommand<DataGrid>(OpenWorkItem);
        private RelayCommand<DataGrid> _openWorkItemCommand;

        public ICommand CopyChangesetCommand => _copyChangesetCommand ??= new RelayCommand<DataGrid>(CopyChangeset);
        private RelayCommand<DataGrid> _copyChangesetCommand;

        public ICommand OpenChangesetCommand => _openChangesetCommand ??= new RelayCommand<DataGrid>(OpenChangeset);
        private RelayCommand<DataGrid> _openChangesetCommand;

        public ICommand GenerateMessageCommand => _generateMessageCommand ??= new RelayCommand<DataGrid>(GenerateMessage);
        private RelayCommand<DataGrid> _generateMessageCommand;

        private void CreateMergeTarget()
        {
            MergeTarget mergeTarget = new MergeTarget {IsCompleted = false};
            DatabaseEngine.MergeTargetCollection.Insert(mergeTarget);
            Model.MergeTargets.Add(mergeTarget);
        }

        private void DeleteMergeTarget(DataGrid dataGrid)
        {
            if (dataGrid.SelectedItem is MergeTarget mergeTarget)
            {
                Model.MergeTargets.Remove(mergeTarget);
                DatabaseEngine.MergeTargetCollection.Delete(mergeTarget.ObjectId);
            }
        }

        private void Delete()
        {
            MessageBoxResult res = MessageBox.Show("Are you sure you want to delete the merge item?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                Model.MergeTargets.ToList().ForEach(t => DatabaseEngine.MergeTargetCollection.Delete(t.ObjectId));
                DatabaseEngine.MergeItemCollection.Delete(Model.ObjectId);
                MergeItem.OnMergeItemDeleted();
            }
        }

        private async void PopulateWorkItemName(DataGrid dataGrid)
        {
            if (dataGrid.SelectedItem is MergeTarget mergeTarget)
            {
                await RootConfiguration.Instance.PerformMergeItemTaskAsync(async () => { await Model.GenerateName(mergeTarget); }, Model);
            }
        }

        private void CopyWorkItemId(DataGrid dataGrid)
        {
            if (dataGrid.SelectedItem is MergeTarget mergeTarget && string.IsNullOrEmpty(mergeTarget.WorkItemId) == false)
            {
                Clipboard.SetData(DataFormats.Text, mergeTarget.WorkItemId);
            }
        }

        private async void OpenWorkItem(DataGrid dataGrid)
        {
            if (dataGrid.SelectedItem is MergeTarget mergeTarget)
            {
                await RootConfiguration.Instance.OpenWorkItem(mergeTarget.WorkItemServer, mergeTarget.WorkItemId, Model);
            }
        }

        private void CopyChangeset(DataGrid dataGrid)
        {
            if (dataGrid.SelectedItem is MergeTarget mergeTarget && string.IsNullOrEmpty(mergeTarget.ChangesetId) == false)
            {
                Clipboard.SetData(DataFormats.Text, mergeTarget.ChangesetId);
            }
        }

        private async void OpenChangeset(DataGrid dataGrid)
        {
            if (dataGrid.SelectedItem is MergeTarget mergeTarget && string.IsNullOrEmpty(mergeTarget.ChangesetId) == false)
            {
                await RootConfiguration.Instance.OpenChangeset(mergeTarget.SourceControlServer, mergeTarget.ChangesetId, Model);
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
