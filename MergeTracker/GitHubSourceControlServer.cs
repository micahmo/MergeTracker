using System.Diagnostics;
using Octokit;
using System.Linq;
using System.Threading.Tasks;

namespace MergeTracker
{
    public class GitHubSourceControlServer : ISourceControlServer
    {
        public string ServerName { get; set; }

        public async Task OpenChangeset(string changesetId)
        {
            Process.Start(await GetChangesetUrl(changesetId));
        }

        public async Task<string> GetChangesetUrl(string changesetId)
        {
            return (await GetClient().Repository.Commit.Get(Org, Repository, changesetId)).HtmlUrl;
        }

        private GitHubClient GetClient()
        {
            if (_gitHubClient != null && _credentials.Password == RootConfiguration.Instance.GitHubToken)
            {
                return _gitHubClient;
            }

            return _gitHubClient = new GitHubClient(new ProductHeaderValue(Org))
            {
                Credentials = _credentials = new Credentials(RootConfiguration.Instance.GitHubToken)
            };
        }
        private GitHubClient _gitHubClient;
        private Credentials _credentials;

        private string Org => ServerName.Split('/').Reverse().Skip(1).FirstOrDefault();

        private string Repository => ServerName.Split('/').LastOrDefault();
    }
}
