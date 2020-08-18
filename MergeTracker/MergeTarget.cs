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

        public bool IsCompleted
        {
            get => _isCompleted;
            set => Set(nameof(IsCompleted), ref _isCompleted, value);
        }
        private bool _isCompleted;

        public string Notes
        {
            get => _notes;
            set => Set(nameof(Notes), ref _notes, value);
        }
        private string _notes;

        public void GenerateCheckinNote(MergeItem mergeItem)
        {
            string originalBugNumber = mergeItem.MergeTargets.FirstOrDefault(t => t.IsOriginal)?.BugNumber ?? "UNKNOWN_BUG";
            string originalVersionNumber = mergeItem.MergeTargets.FirstOrDefault(t => t.IsOriginal)?.TargetBranch ?? "UNKNOWN_VERSION";
            string targetVersionNumber = TargetBranch ?? "UNKNOWN_TARGET_BRANCH";

            string result = $"Merge fix for {originalBugNumber} ({originalVersionNumber}) into {targetVersionNumber}.";

            Clipboard.SetData(DataFormats.Text, result);
        }

        public void Save()
        {
            DatabaseEngine.MergeTargetCollection.Update(this);
        }
    }
}
