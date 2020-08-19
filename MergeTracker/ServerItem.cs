using System.Windows.Input;
using GalaSoft.MvvmLight.Command;

namespace MergeTracker
{
    public abstract class ServerItem
    {
        public string ServerName { get; set; }

        public bool IsSelected { get; set; }

        public MergeTarget MergeTarget { get; set; }

        public override string ToString() => ServerName;

        public ICommand Command => _command ??= new RelayCommand(Select);
        private RelayCommand _command;

        public abstract void Select();
    }

    public class SourceControlServerItem : ServerItem
    {
        public override void Select()
        {
            MergeTarget.SourceControlServer = ServerName;
            MergeTarget.RaisePropertyChanged(nameof(MergeTarget.SourceControlServers));
        }
    }

    public class WorkItemServerItem : ServerItem
    {
        public override void Select()
        {
            MergeTarget.WorkItemServer = ServerName;
            MergeTarget.RaisePropertyChanged(nameof(MergeTarget.WorkItemServers));
        }
    }
}
