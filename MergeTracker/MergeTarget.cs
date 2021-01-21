using System.Collections.Generic;
using System.Linq;
using System.Windows;
using GalaSoft.MvvmLight;
using LiteDB;

namespace MergeTracker
{
    public class MergeTarget : ObservableObject
    {
        [BsonId]
        public int ObjectId { get; set; }

        public string TargetBranch
        {
            get => _targetBranch;
            set => Set(nameof(TargetBranch), ref _targetBranch, value);
        }
        private string _targetBranch;

        public string BugNumber
        {
            get => _bugNumber;
            set => Set(nameof(BugNumber), ref _bugNumber, value);
        }
        private string _bugNumber;

        public string Changeset
        {
            get => _changeset;
            set => Set(nameof(Changeset), ref _changeset, value);
        }
        private string _changeset;

        public bool IsOriginal
        {
            get => _isOriginal;
            set => Set(nameof(IsOriginal), ref _isOriginal, value);
        }
        private bool _isOriginal;

        // A note about this nullable value.
        // LiteDB only assigns the value during serialization if it is non-null in the DB.
        // Originally, the _isCompleted field was set to false for the sake of new instances of MergeTarget,
        // but then it stayed false during deserialization if the incoming value was null.
        // Going forward, new instances of MergeTarget should manually set IsCompleted to the preferred default value (probably false).
        public bool? IsCompleted
        {
            get => _isCompleted;
            set => Set(nameof(IsCompleted), ref _isCompleted, value);
        }
        private bool? _isCompleted;

        public string Notes
        {
            get => _notes;
            set => Set(nameof(Notes), ref _notes, value);
        }
        private string _notes;

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

        [BsonIgnore]
        public IEnumerable<ServerItem> WorkItemServers => RootConfiguration.Instance.WorkItemServers?.Select(s => new WorkItemServerItem { ServerName = s, IsSelected = s == WorkItemServer, MergeTarget = this });

        [BsonIgnore]
        public IEnumerable<ServerItem> SourceControlServers => RootConfiguration.Instance.SourceControlServers?.Select(s => new SourceControlServerItem { ServerName = s, IsSelected = s == SourceControlServer, MergeTarget = this });

        public void GenerateCheckinNote(MergeItem mergeItem)
        {
            string originalBugNumber = mergeItem.MergeTargets.FirstOrDefault(t => t.IsOriginal)?.BugNumber;
            string originalVersionNumber = mergeItem.MergeTargets.FirstOrDefault(t => t.IsOriginal)?.TargetBranch;

            string result = RootConfiguration.Instance.CheckInMessage
                .Replace("%o", originalBugNumber)
                .Replace("%v", originalVersionNumber)
                .Replace("%b", TargetBranch)
                .Replace("%t", BugNumber);

            Clipboard.SetData(DataFormats.Text, result);
        }

        public void Save()
        {
            DatabaseEngine.MergeTargetCollection.Update(this);
        }
    }
}
