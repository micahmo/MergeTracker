using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;

namespace MergeTracker
{
    public class OnPremTfsWorkItemServer : IWorkItemServer
    {
        public string ServerName { get; set; }
        
        public async Task OpenWorkItem(string workItemId)
        {
            Process.Start(await GetWorkItemUrl(workItemId));
        }

        public async Task<string> GetWorkItemUrl(string workItemId)
        {
            if (await GetWorkItem(workItemId) is { } workItem)
            {
                if (workItem.Links.Links.TryGetValue("html", out var html) && html is ReferenceLink htmlReferenceLink)
                {
                    return htmlReferenceLink.Href;
                }
            }

            throw new Exception($"Unable to access work item ID {workItemId} on server {ServerName}.");
        }

        public async Task<string> GetWorkItemTitle(string workItemId)
        {
            return (await GetWorkItem(workItemId))?.Fields["System.Title"]?.ToString();
        }

        private Task<WorkItem> GetWorkItem(string workItemId)
        {
            if (int.TryParse(workItemId, out int workItemIdNumber))
            {
                return GetClient().GetWorkItemAsync(workItemIdNumber);
            }

            throw new Exception("On-prem TFS work item IDs must be integers.");
        }

        private WorkItemTrackingHttpClient GetClient()
        {
            if (_client != null && _networkCredential.UserName == RootConfiguration.Instance.OnPremTfsUsername && _networkCredential.Password == RootConfiguration.Instance.OnPremTfsPassword)
            {
                return _client;
            }

            VssCredentials vssCredentials = new VssCredentials(new WindowsCredential(_networkCredential = new NetworkCredential(RootConfiguration.Instance.OnPremTfsUsername, RootConfiguration.Instance.OnPremTfsPassword)));
            VssConnection vssConnection = new VssConnection(new Uri(ServerName), vssCredentials);
            return _client = vssConnection.GetClient<WorkItemTrackingHttpClient>();
        }
        private WorkItemTrackingHttpClient _client;
        private NetworkCredential _networkCredential;
    }
}
