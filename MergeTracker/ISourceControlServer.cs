using System.Threading.Tasks;

namespace MergeTracker
{
    public interface ISourceControlServer
    {
        string ServerName { get; set; }

        Task OpenChangeset(string changesetId);

        Task<string> GetChangesetUrl(string changesetId);
    }
}
