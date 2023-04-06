using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

namespace MergeTracker
{
    public class OnPremTfsSourceControlServer : ISourceControlServer
    {
        public string ServerName { get; set; }
        
        public async Task OpenChangeset(string changesetId)
        {
            Process.Start(await GetChangesetUrl(changesetId));
        }

        public async Task<string> GetChangesetUrl(string changesetId)
        {
            if (await GetChangeset(changesetId) is TfvcChangeset changeset)
            {
                if (changeset.Links.Links.TryGetValue("web", out var html) && html is ReferenceLink htmlReferenceLink)
                {
                    return htmlReferenceLink.Href;
                }
            }

            throw new Exception($"Unable to access changeset ID {changesetId} on server {ServerName}.");
        }

        private Task<TfvcChangeset> GetChangeset(string changesetId)
        {
            if (int.TryParse(changesetId, out int changesetIdNumber))
            {
                return GetClient().GetChangesetAsync(changesetIdNumber);
            }

            throw new Exception("On-prem TFS changeset IDs must be integers.");
        }

        private TfvcHttpClient GetClient()
        {
            if (_client != null && _networkCredential.UserName == RootConfiguration.Instance.OnPremTfsUsername && _networkCredential.Password == RootConfiguration.Instance.OnPremTfsPassword)
            {
                return _client;
            }

            VssCredentials vssCredentials = new VssCredentials(new WindowsCredential(_networkCredential = new NetworkCredential(RootConfiguration.Instance.OnPremTfsUsername, RootConfiguration.Instance.OnPremTfsPassword)));
            VssConnection vssConnection = new VssConnection(new Uri(ServerName), vssCredentials);
            return _client = vssConnection.GetClient<TfvcHttpClient>();
        }
        private TfvcHttpClient _client;
        private NetworkCredential _networkCredential;
    }
}
