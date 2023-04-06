using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MergeTracker
{
    public class OnPremTfsGitSourceControlServer : ISourceControlServer
    {
        public string ServerName { get; set; }

        public async Task OpenChangeset(string changesetId)
        {
            Process.Start(await GetChangesetUrl(changesetId));
        }

        public async Task<string> GetChangesetUrl(string changesetId)
        {
            if (await GetCommit(changesetId) is { } commit)
            {
                if (commit.Links.Links.TryGetValue("web", out var html) && html is ReferenceLink htmlReferenceLink)
                {
                    return htmlReferenceLink.Href;
                }
            }

            throw new Exception($"Unable to access commit ID {changesetId} on server {ServerName}.");
        }

        private Task<GitCommit> GetCommit(string commitId)
        {
            return GetClient().GetCommitAsync(ProjectName, commitId, RepoName);
        }

        private GitHttpClient GetClient()
        {
            if (_client != null && _networkCredential.UserName == RootConfiguration.Instance.OnPremTfsUsername && _networkCredential.Password == RootConfiguration.Instance.OnPremTfsPassword)
            {
                return _client;
            }

            VssCredentials vssCredentials = new VssCredentials(new WindowsCredential(_networkCredential = new NetworkCredential(RootConfiguration.Instance.OnPremTfsUsername, RootConfiguration.Instance.OnPremTfsPassword)));
            VssConnection vssConnection = new VssConnection(new Uri(ActualServerName), vssCredentials);
            return _client = vssConnection.GetClient<GitHttpClient>();
        }
        private GitHttpClient _client;
        private NetworkCredential _networkCredential;

        private string ActualServerName => ServerName.Split('|').Skip(0).FirstOrDefault();

        private string ProjectName => ServerName.Split('|').Skip(1).FirstOrDefault();

        private string RepoName => ServerName.Split('|').Skip(2).FirstOrDefault();
    }
}
