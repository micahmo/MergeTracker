using System.Diagnostics;
using Octokit;
using System.Threading.Tasks;
using System.Linq;
using ProductHeaderValue = Octokit.ProductHeaderValue;
using System;

namespace MergeTracker
{
    public class GitHubWorkItemServer : IWorkItemServer
    {
        public string ServerName { get; set; }

        public async Task OpenWorkItem(string workItemId)
        {
            Process.Start(await GetWorkItemUrl(workItemId));
        }

        public async Task<string> GetWorkItemUrl(string workItemId)
        {
            return (await GetPullRequest(workItemId)).HtmlUrl;
        }

        public async Task<string> GetWorkItemTitle(string workItemId)
        {
            return (await GetPullRequest(workItemId)).Title;
        }

        private Task<PullRequest> GetPullRequest(string workItemId)
        {
            if (int.TryParse(workItemId, out int pullRequestNumber))
            {
                return GetClient().PullRequest.Get(Org, Repository, pullRequestNumber);
            }

            throw new Exception("GitHub work item IDs must be integers.");
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
