using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
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
                case nameof(MergeTarget.IsOriginal):
                    SetOriginal(sender as MergeTarget);
                    break;
            }
        }

        private async void GenerateName(MergeTarget mergeTarget)
        {
            try
            {
                if (RootConfiguration.Instance.UseTfs &&
                    Name == "New Merge Item" && mergeTarget?.IsOriginal == true && int.TryParse(mergeTarget.BugNumber, out int bugNumber))
                {
                    Name = (await TfsUtils.GetWorkItem(WorkItemServer, bugNumber))?.Fields["System.Title"]?.ToString() ?? Name;
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

        public string WorkItemServer
        {
            get => _workItemServer ?? RootConfiguration.Instance.DefaultWorkItemServer;
            set => Set(nameof(WorkItemServer), ref _workItemServer, value);
        }
        private string _workItemServer;

        public string SourceControlServer
        {
            get => _sourceControlSerer ?? RootConfiguration.Instance.DefaultSourceControlServer;
            set => Set(nameof(SourceControlServer), ref _sourceControlSerer, value);
        }
        private string _sourceControlSerer;

        [JsonIgnore]
        public IEnumerable<ServerItem> WorkItemServers => RootConfiguration.Instance.WorkItemServers?.Select(s => new WorkItemServerItem {ServerName = s, IsSelected = s == WorkItemServer, MergeItem = this});

        [JsonIgnore]
        public IEnumerable<ServerItem> SourceControlServers => RootConfiguration.Instance.SourceControlServers?.Select(s => new SourceControlServerItem { ServerName = s, IsSelected = s == SourceControlServer, MergeItem = this });

        [JsonIgnore]
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

        [JsonIgnore]
        public bool HasLastError => string.IsNullOrEmpty(LastError) == false;
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

        private async void OpenBug(DataGrid dataGrid)
        {
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                if (dataGrid.SelectedItem is MergeTarget mergeTarget && int.TryParse(mergeTarget.BugNumber, out int bugNumber))
                {
                    if (await TfsUtils.GetWorkItem(Model.WorkItemServer, bugNumber) is { } workItem)
                    {
                        if (workItem.Links.Links.TryGetValue("html", out var html) && html is ReferenceLink htmlReferenceLink)
                        {
                            Process.Start(htmlReferenceLink.Href);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Model.LastError = $"There was an error opening work item.\n\n{ex}";
            }

            Mouse.OverrideCursor = null;
        }

        private async void OpenChangeset(DataGrid dataGrid)
        {
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                if (dataGrid.SelectedItem is MergeTarget mergeTarget && string.IsNullOrEmpty(mergeTarget.Changeset) == false)
                {
                    foreach (string changesetString in mergeTarget.Changeset.Split(new[] {",", ";"}, StringSplitOptions.RemoveEmptyEntries))
                    {
                        // Check if it's a TFS changeset or a Git commit
                        if (changesetString.Any(c => char.IsLetter(c)))
                        {
                            // Git commit -- we need server name and project name
                            if (Model.SourceControlServer.Split(new[] {"|"}, StringSplitOptions.RemoveEmptyEntries) is { } parts && parts.Length == 3)
                            {
                                if (await TfsUtils.GetCommit(parts[0], parts[1], parts[2], changesetString) is GitCommit commit)
                                {
                                    if (commit.Links.Links.TryGetValue("web", out var html) && html is ReferenceLink htmlReferenceLink)
                                    {
                                        Process.Start(htmlReferenceLink.Href);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // TFS changeset
                            if (int.TryParse(changesetString, out int changesetId) && await TfsUtils.GetChangeset(Model.SourceControlServer, changesetId) is TfvcChangeset changeset)
                            {
                                if (changeset.Links.Links.TryGetValue("web", out var html) && html is ReferenceLink htmlReferenceLink)
                                {
                                    Process.Start(htmlReferenceLink.Href);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Model.LastError = $"There was an error opening changeset.\n\n{ex}";
            }

            Mouse.OverrideCursor = null;
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
