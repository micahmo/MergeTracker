using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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

        public bool ShowProjectSettings
        {
            get => _showProjectSettings;
            set => Set(nameof(ShowProjectSettings), ref _showProjectSettings, value);
        }
        private bool _showProjectSettings;

        #region On-Prem TFS

        public string OnPremTfsUsername
        {
            get => _onPremTfsUsername;
            set => Set(nameof(OnPremTfsUsername), ref _onPremTfsUsername, value);
        }
        private string _onPremTfsUsername;

        public string OnPremTfsPassword
        {
            get => _onPremTfsPassword;
            set => Set(nameof(OnPremTfsPassword), ref _onPremTfsPassword, value);
        }
        private string _onPremTfsPassword;

        public string OnPremTfsWorkItemServers
        {
            get => _onPremTfsWorkItemServers ?? string.Empty;
            set
            {
                Set(nameof(OnPremTfsWorkItemServers), ref _onPremTfsWorkItemServers, value);
                RaisePropertyChanged(nameof(WorkItemServers));
            }
        }
        private string _onPremTfsWorkItemServers;

        public string OnPremTfsSourceControlServers
        {
            get => _onPremTfsSourceControlServers ?? string.Empty;
            set
            {
                Set(nameof(OnPremTfsSourceControlServers), ref _onPremTfsSourceControlServers, value);
                RaisePropertyChanged(nameof(SourceControlServers));
            }
        }
        private string _onPremTfsSourceControlServers;

        public string OnPremTfsGitSourceControlServers
        {
            get => _onPremTfsGitSourceControlServers ?? string.Empty;
            set
            {
                Set(nameof(OnPremTfsGitSourceControlServers), ref _onPremTfsGitSourceControlServers, value);
                RaisePropertyChanged(nameof(SourceControlServers));
            }
        }
        private string _onPremTfsGitSourceControlServers;

        #endregion

        #region Cloud AzureDevOps

        public string CloudAzureDevOpsToken
        {
            get => _cloudAzureDevOpsToken;
            set => Set(nameof(CloudAzureDevOpsToken), ref _cloudAzureDevOpsToken, value);
        }
        private string _cloudAzureDevOpsToken;

        public string CloudAzureDevOpsWorkItemServers
        {
            get => _cloudAzureDevOpsWorkItemServers ?? string.Empty;
            set
            {
                Set(nameof(CloudAzureDevOpsWorkItemServers), ref _cloudAzureDevOpsWorkItemServers, value);
                RaisePropertyChanged(nameof(SourceControlServers));
            }
        }
        private string _cloudAzureDevOpsWorkItemServers;

        #endregion

        #region GitHub

        public string GitHubToken
        {
            get => _gitHubToken;
            set => Set(nameof(GitHubToken), ref _gitHubToken, value);
        }
        private string _gitHubToken;

        public string GitHubWorkItemServers
        {
            get => _gitHubWorkItemServers ?? string.Empty;
            set
            {
                Set(nameof(GitHubWorkItemServers), ref _gitHubWorkItemServers, value);
                RaisePropertyChanged(nameof(SourceControlServers));
            }
        }
        private string _gitHubWorkItemServers;

        public string GitHubSourceControlServers
        {
            get => _gitHubSourceControlServers ?? string.Empty;
            set
            {
                Set(nameof(GitHubSourceControlServers), ref _gitHubSourceControlServers, value);
                RaisePropertyChanged(nameof(SourceControlServers));
            }
        }
        private string _gitHubSourceControlServers;

        #endregion

        #region Jira

        public string JiraUsername
        {
            get => _jiraUsername;
            set => Set(nameof(JiraUsername), ref _jiraUsername, value);
        }
        private string _jiraUsername;

        public string JiraPassword
        {
            get => _jiraPassword;
            set => Set(nameof(JiraPassword), ref _jiraPassword, value);
        }
        private string _jiraPassword;

        public string JiraWorkItemServers
        {
            get => _jiraWorkItemServers ?? string.Empty;
            set
            {
                Set(nameof(JiraWorkItemServers), ref _jiraWorkItemServers, value);
                RaisePropertyChanged(nameof(SourceControlServers));
            }
        }
        private string _jiraWorkItemServers;

        #endregion

        [BsonIgnore]
        public List<string> WorkItemServers => $"{OnPremTfsWorkItemServers};{CloudAzureDevOpsWorkItemServers};{GitHubWorkItemServers};{JiraWorkItemServers}".Split(new[] { ",", ";" }, StringSplitOptions.RemoveEmptyEntries).ToList();

        [BsonIgnore]
        public IEnumerable<WorkItemServerItem> WorkItemServersBinding => WorkItemServers.Select(s => new WorkItemServerItem { ServerName = s, IsSelected = s == DefaultWorkItemServer });

        public string DefaultWorkItemServer
        {
            get => _defaultWorkItemServer ?? WorkItemServers?.FirstOrDefault();
            set
            {
                Set(nameof(DefaultWorkItemServer), ref _defaultWorkItemServer, value);
                RaisePropertyChanged(nameof(DefaultServersToolTip));
            }
        }

        private string _defaultWorkItemServer;

        public string SelectedWorkItemServer
        {
            get => _workItemServer ?? DefaultWorkItemServer;
            set => Set(nameof(SelectedWorkItemServer), ref _workItemServer, value);
        }
        private string _workItemServer;

        [BsonIgnore]
        public bool HasAnyWorkItemServers => WorkItemServers.Any();

        [BsonIgnore]
        public List<string> SourceControlServers => $"{OnPremTfsSourceControlServers};{OnPremTfsGitSourceControlServers};{GitHubSourceControlServers}".Split(new[] { ",", ";" }, StringSplitOptions.RemoveEmptyEntries).ToList();

        [BsonIgnore]
        public IEnumerable<SourceControlServerItem> SourceControlServersBinding => SourceControlServers.Select(s => new SourceControlServerItem { ServerName = s, IsSelected = s == DefaultSourceControlServer });

        public string DefaultSourceControlServer
        {
            get => _defaultSourceControlServer ?? SourceControlServers?.FirstOrDefault();
            set
            {
                Set(nameof(DefaultSourceControlServer), ref _defaultSourceControlServer, value);
                RaisePropertyChanged(nameof(DefaultServersToolTip));
            }
        }

        private string _defaultSourceControlServer;

        public string SelectedSourceControlServer
        {
            get => _sourceControlSerer ?? DefaultSourceControlServer;
            set => Set(nameof(SelectedSourceControlServer), ref _sourceControlSerer, value);
        }
        private string _sourceControlSerer;

        [BsonIgnore]
        public bool HasAnySourceControlServers => SourceControlServers.Any();

        [BsonIgnore]
        public string DefaultServersToolTip => $"Default Work Item Server: {DefaultWorkItemServer}\nDefault Source Control Server: {DefaultSourceControlServer}";

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

        public string CheckInMessage
        {
            get => _checkInMessage;
            set
            {
                Set(nameof(CheckInMessage), ref _checkInMessage, value);
                RaisePropertyChanged(nameof(SampleCheckInMessage));
            }
        }
        private string _checkInMessage = "For %t: Merge fix for %o (%v) into %b.";

        public string SampleCheckInMessage =>
            CheckInMessage.Replace("%t", "234567").Replace("%o", "111222").Replace("%v", "2.80").Replace("%b", "2.90");

        public async Task<string> PerformMergeItemTaskAsync(Func<Task> taskToPerform, MergeItem mergeItem = null)
        {
            string result = default;

            Mouse.OverrideCursor = Cursors.Wait;

            // Clear the last error
            if (mergeItem is { })
            {
                mergeItem.LastError = default;
            }

            try
            {
                await taskToPerform();
            }
            catch (Exception ex)
            {
                result = $"There was an error performing the task.\n\n{ex}";

                if (mergeItem is { })
                {
                    mergeItem.LastError = result;
                }
            }

            Mouse.OverrideCursor = null;

            return result;
        }

        public Task<string> OpenWorkItem(string workItemServer, string workItemId, MergeItem mergeItem = null)
        {
            return PerformMergeItemTaskAsync(async () =>
            {
                foreach (string workItemIdString in workItemId.Split(new[] { ",", ";" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    await GetWorkItemServer(workItemServer).OpenWorkItem(workItemIdString);
                }
            }, mergeItem);
        }

        public Task<string> OpenChangeset(string sourceControlServer, string changeset, MergeItem mergeItem = null)
        {
            return PerformMergeItemTaskAsync(async () =>
            {
                foreach (string changesetString in changeset.Split(new[] {",", ";"}, StringSplitOptions.RemoveEmptyEntries))
                {
                    await GetSourceControlServer(sourceControlServer).OpenChangeset(changesetString);
                }
            }, mergeItem);
        }

        public Task<string> CopyWorkItemUrl(string workItemServer, string workIdemId, MergeItem mergeItem = null)
        {
            return PerformMergeItemTaskAsync(async () =>
            {
                Clipboard.SetText(await GetWorkItemServer(workItemServer).GetWorkItemUrl(workIdemId));
            }, mergeItem);
        }

        public Task<string> CopyChangesetUrl(string sourceControlServer, string changeset, MergeItem mergeItem = null)
        {
            return PerformMergeItemTaskAsync(async () =>
            {
                Clipboard.SetText(await  GetSourceControlServer(sourceControlServer).GetChangesetUrl(changeset));
            }, mergeItem);
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
            nameof(MergeTarget.WorkItemId) => Filter,
            nameof(MergeTarget.ChangesetId) => Filter,
            nameof(MergeTarget.TargetBranch) => Filter,
            _ => string.Empty
        };

        public IWorkItemServer GetWorkItemServer(string serverName)
        {
            if (_workItemServerInstances.ContainsKey(serverName))
            {
                return _workItemServerInstances[serverName];
            }

            // Figure out what kind of server this is and make a new one
            if (OnPremTfsWorkItemServers.Contains(serverName))
            {
                return _workItemServerInstances[serverName] = new OnPremTfsWorkItemServer { ServerName = serverName };
            }

            if (CloudAzureDevOpsWorkItemServers.Contains(serverName))
            {
                return _workItemServerInstances[serverName] = new CloudAzureDevOpsWorkItemServer { ServerName = serverName };
            }

            if (GitHubWorkItemServers.Contains(serverName))
            {
                return _workItemServerInstances[serverName] = new GitHubWorkItemServer { ServerName = serverName };
            }

            if (JiraWorkItemServers.Contains(serverName))
            {
                return _workItemServerInstances[serverName] = new JiraWorkItemServer { ServerName = serverName };
            }

            return null;
        }
        private readonly Dictionary<string, IWorkItemServer> _workItemServerInstances = new Dictionary<string, IWorkItemServer>();

        public ISourceControlServer GetSourceControlServer(string serverName)
        {
            if (_sourceControlServerInstances.ContainsKey(serverName))
            {
                return _sourceControlServerInstances[serverName];
            }

            // Figure out what kind of server this is and make a new one
            if (OnPremTfsSourceControlServers.Contains(serverName))
            {
                return _sourceControlServerInstances[serverName] = new OnPremTfsSourceControlServer { ServerName = serverName };
            }
            
            if (OnPremTfsGitSourceControlServers.Contains(serverName))
            {
                return _sourceControlServerInstances[serverName] = new OnPremTfsGitSourceControlServer { ServerName = serverName };
            }

            if (GitHubSourceControlServers.Contains(serverName))
            {
                return _sourceControlServerInstances[serverName] = new GitHubSourceControlServer { ServerName = serverName };
            }

            return null;
        }
        private readonly Dictionary<string, ISourceControlServer> _sourceControlServerInstances = new Dictionary<string, ISourceControlServer>();

        #endregion
    }
}
