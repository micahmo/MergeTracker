using System.Threading.Tasks;

namespace MergeTracker
{
    public interface IWorkItemServer
    {
        string ServerName { get; set; }

        Task OpenWorkItem(string workItemId);

        Task<string> GetWorkItemUrl(string workItemId);

        Task<string> GetWorkItemTitle(string workItemId);
    }
}
