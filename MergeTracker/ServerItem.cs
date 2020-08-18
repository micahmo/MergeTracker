using System.Windows.Input;
using GalaSoft.MvvmLight.Command;

namespace MergeTracker
{
    public abstract class ServerItem
    {
        public string ServerName { get; set; }

        public bool IsSelected { get; set; }

        public MergeItem MergeItem { get; set; }

        public override string ToString() => ServerName;

        public ICommand Command => _command ??= new RelayCommand(Select);
        private RelayCommand _command;

        public abstract void Select();
    }

    public class SourceControlServerItem : ServerItem
    {
        public override void Select()
        {
            MergeItem.SourceControlServer = ServerName;
            MergeItem.RaisePropertyChanged(nameof(MergeTracker.MergeItem.SourceControlServers));
        }
    }

    public class WorkItemServerItem : ServerItem
    {
        public override void Select()
        {
            MergeItem.WorkItemServer = ServerName;
            MergeItem.RaisePropertyChanged(nameof(MergeTracker.MergeItem.WorkItemServers));
        }
    }
}
