﻿using System.Diagnostics;
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
            var url = (await GetClient().Git.Commit.Get(Org, Repository, changesetId)).Url;
            url = url.Replace("api.", string.Empty).Replace("repos/", string.Empty).Replace("git/", string.Empty).Replace("commits/", "commit/");
            return url;
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
