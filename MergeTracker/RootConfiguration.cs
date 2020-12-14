using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using LiteDB;

namespace MergeTracker
{
    public class RootConfiguration : ObservableObject, ITextData
    {
        public RootConfiguration()
        {
            PropertyChanged += RootConfiguration_PropertyChanged;
        }

        private void RootConfiguration_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(WorkItemServers):
                    MergeItems.ToList().ForEach(i => i.MergeTargets.ToList().ForEach(t => t.RaisePropertyChanged(nameof(t.WorkItemServers))));
                    break;
                case nameof(SourceControlServers):
                    MergeItems.ToList().ForEach(i => i.MergeTargets.ToList().ForEach(t => t.RaisePropertyChanged(nameof(t.SourceControlServers))));
                    break;
            }
        }

        public static RootConfiguration Instance { get; private set; }

        [BsonId]
        public int ObjectId { get; set; }

        [BsonIgnore]
        public ObservableCollection<MergeItem> MergeItems { get; set; } = new ObservableCollection<MergeItem>();

        public string Filter
        {
            get => _filter?.Trim();
            set => Set(nameof(Filter), ref _filter, value);
        }
        private string _filter;

        public bool NotCompletedFilter
        {
            get => _notCompletedFilter;
            set => Set(nameof(NotCompletedFilter), ref _notCompletedFilter, value);
        }
        private bool _notCompletedFilter;

        public bool ShowTfsSettings
        {
            get => _showTfsSettings;
            set => Set(nameof(ShowTfsSettings), ref _showTfsSettings, value);
        }
        private bool _showTfsSettings;

        public bool UseTfs
        {
            get => _useTfs;
            set => Set(nameof(UseTfs), ref _useTfs, value);
        }
        private bool _useTfs;

        public string TfsUsername
        {
            get => _tfsUsername;
            set => Set(nameof(TfsUsername), ref _tfsUsername, value);
        }
        private string _tfsUsername;

        public string TfsPassword
        {
            get => _tfsPassword;
            set => Set(nameof(TfsPassword), ref _tfsPassword, value);
        }
        private string _tfsPassword;

        public List<string> WorkItemServers
        {
            get => DelimitedWorkItemServers?.Split(new[] {";"}, StringSplitOptions.RemoveEmptyEntries).ToList();
            set => DelimitedWorkItemServers = string.Join(";", value ?? Enumerable.Empty<string>());
        }

        [BsonIgnore]
        public string DelimitedWorkItemServers
        {
            get => _delimitedWorkItemServers;
            set
            {
                Set(nameof(DelimitedWorkItemServers), ref _delimitedWorkItemServers, value);
                RaisePropertyChanged(nameof(WorkItemServers));
            }
        }
        private string _delimitedWorkItemServers;

        [BsonIgnore]
        public string DefaultWorkItemServer => WorkItemServers?.FirstOrDefault();

        public string SelectedWorkItemServer
        {
            get => _workItemServer ?? DefaultWorkItemServer;
            set => Set(nameof(SelectedWorkItemServer), ref _workItemServer, value);
        }
        private string _workItemServer;

        public List<string> SourceControlServers
        {
            get => DelimitedSourceControlServers?.Split(new[] {";"}, StringSplitOptions.RemoveEmptyEntries).ToList();
            set => DelimitedSourceControlServers = string.Join(";", value ?? Enumerable.Empty<string>());
        }

        [BsonIgnore]
        public string DelimitedSourceControlServers
        {
            get => _delimitedSourceControlServers;
            set
            {
                Set(nameof(DelimitedSourceControlServers), ref _delimitedSourceControlServers, value);
                RaisePropertyChanged(nameof(SourceControlServers));
            }
        }
        private string _delimitedSourceControlServers;

        [BsonIgnore]
        public string DefaultSourceControlServer => SourceControlServers?.FirstOrDefault();

        public string SelectedSourceControlServer
        {
            get => _sourceControlSerer ?? DefaultSourceControlServer;
            set => Set(nameof(SelectedSourceControlServer), ref _sourceControlSerer, value);
        }
        private string _sourceControlSerer;

        public ItemType SelectedItemType
        {
            get => _selectedItemType;
            set => Set(nameof(SelectedItemType), ref _selectedItemType, value);
        }
        private ItemType _selectedItemType;

        public string SelectedItemId
        {
            get => _selectedItemId?.Trim();
            set => Set(nameof(SelectedItemId), ref _selectedItemId, value);
        }
        private string _selectedItemId;

        public async Task<bool> OpenBug(string workItemServer, int bugNumber, MergeItem mergeItem = null)
        {
            bool result = false;

            if (UseTfs)
            {
                Mouse.OverrideCursor = Cursors.Wait;

                try
                {
                    await TfsUtils.OpenWorkItem(workItemServer, bugNumber);
                    result = true;
                }
                catch (Exception ex)
                {
                    if (mergeItem is { })
                    {
                        mergeItem.LastError = $"There was an error opening work item.\n\n{ex}";
                    }
                }

                Mouse.OverrideCursor = null;
            }

            return result;
        }

        public async Task<bool> OpenChangeset(string sourceControlServer, string changeset, MergeItem mergeItem = null)
        {
            bool result = false;

            if (UseTfs)
            {
                Mouse.OverrideCursor = Cursors.Wait;

                try
                {
                    foreach (string changesetString in changeset.Split(new[] { ",", ";" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        // Check if it's a TFS changeset or a Git commit
                        if (changesetString.Any(char.IsLetter))
                        {
                            // Git commit -- we need server name and project name
                            if (sourceControlServer.Split(new[] { "|" }, StringSplitOptions.RemoveEmptyEntries) is { } parts && parts.Length == 3)
                            {
                                await TfsUtils.OpenCommit(parts[0], parts[1], parts[2], changesetString);
                                result = true;
                            }
                        }
                        else
                        {
                            // TFS changeset
                            if (int.TryParse(changesetString, out int changesetId))
                            {
                                await TfsUtils.OpenChangeset(sourceControlServer, changesetId);
                                result = true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (mergeItem is { })
                    {
                        mergeItem.LastError = $"There was an error opening changeset.\n\n{ex}";
                    }
                }

                Mouse.OverrideCursor = null;
            }

            return result;
        }

        public static RootConfiguration Load()
        {
            if (DatabaseEngine.RootConfigurationCollection.Query().FirstOrDefault() is { } rootConfiguration)
            {
                Instance = rootConfiguration;
            }
            else
            {
                DatabaseEngine.RootConfigurationCollection.Insert(Instance = new RootConfiguration());
            }

            return Instance;
        }

        public void Save()
        {
            Instance.MergeItems.ToList().ForEach(i => i.Save());
            DatabaseEngine.RootConfigurationCollection.Update(this);
        }

        #region ITextData members

        /// <inheritdoc/>
        public string GetTextData(string key) => key switch
        {
            nameof(MergeItem.Name) => Filter,
            nameof(MergeTarget.BugNumber) => Filter,
            nameof(MergeTarget.Changeset) => Filter,
            nameof(MergeTarget.TargetBranch) => Filter,
            _ => string.Empty
        };

        #endregion
    }
}
