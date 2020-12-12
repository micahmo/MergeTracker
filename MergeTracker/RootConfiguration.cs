using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
