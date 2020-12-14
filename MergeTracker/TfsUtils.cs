using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace MergeTracker
{
    public static class TfsUtils
    {
        private static Task<TClient> Initialize<TClient>(string serverName) where TClient : VssHttpClientBase
        {
            return Task.Run(() =>
            {
                TClient result = null;
                ServerClientTypePair serverClientTypePair = new ServerClientTypePair {ServerName = serverName, VssHttpClient = typeof(TClient)};

                if (_failedToConnect.Contains(serverName) == false) // Do not try to connect a second time
                {
                    if (_tfsClients.TryGetValue(serverClientTypePair, out var cachedTfsClient) && cachedTfsClient is TClient client)
                    {
                        result = client;
                    }
                    else
                    {
                        try
                        {
                            NetworkCredential nc = new NetworkCredential(RootConfiguration.Instance.TfsUsername, RootConfiguration.Instance.TfsPassword);
                            VssCredentials vssCredentials = new VssCredentials(new WindowsCredential(nc));
                            VssConnection vssConnection = new VssConnection(new Uri(serverName), vssCredentials);

                            // Trust all certificates. This is usually a bad idea!! But our TFS servers often have bad certs...
                            ServicePointManager.ServerCertificateValidationCallback = (_, __, ___, ____) => true;

                            _tfsClients[serverClientTypePair] = result = vssConnection.GetClient<TClient>();
                        }
                        catch
                        {
                            _failedToConnect.Add(serverName);
                            throw;
                        }
                    }
                }

                return result;
            });
        }

        public static Task<WorkItem> GetWorkItem(string serverName, int workItemId)
        {
            return Task.Run(async () =>
            {
                if (await Initialize<WorkItemTrackingHttpClient>(serverName) is { } workItemStore)
                {
                    return await workItemStore.GetWorkItemAsync(workItemId);
                }

                return null;
            });
        }

        public static async Task OpenWorkItem(string serverName, int workItemId)
        {
            if (await GetWorkItem(serverName, workItemId) is { } workItem)
            {
                if (workItem.Links.Links.TryGetValue("html", out var html) && html is ReferenceLink htmlReferenceLink)
                {
                    Process.Start(htmlReferenceLink.Href);
                }
            }
        }

        public static Task<TfvcChangeset> GetChangeset(string serverName, int changesetId)
        {
            return Task.Run(async () =>
            {
                if (await Initialize<TfvcHttpClient>(serverName) is TfvcHttpClient versionControl)
                {
                    return await versionControl.GetChangesetAsync(changesetId);
                }

                return null;
            });
        }

        public static Task<GitCommit> GetCommit(string serverName, string projectName, string repositoryName, string commitId)
        {
            return Task.Run(async () =>
            {
                if (await Initialize<GitHttpClient>(serverName) is GitHttpClient versionControl)
                {
                    return await versionControl.GetCommitAsync(projectName, commitId, repositoryName);
                }

                return null;
            });
        }

        public static async Task OpenChangeset(string serverName, int changesetId)
        {
            if (await GetChangeset(serverName, changesetId) is TfvcChangeset changeset)
            {
                if (changeset.Links.Links.TryGetValue("web", out var html) && html is ReferenceLink htmlReferenceLink)
                {
                    Process.Start(htmlReferenceLink.Href);
                }
            }
        }

        public static async Task OpenCommit(string serverName, string projectName, string repositoryName, string commitId)
        {
            if (await GetCommit(serverName, projectName, repositoryName, commitId) is GitCommit commit)
            {
                if (commit.Links.Links.TryGetValue("web", out var html) && html is ReferenceLink htmlReferenceLink)
                {
                    Process.Start(htmlReferenceLink.Href);
                }
            }
        }

        private static readonly HashSet<string> _failedToConnect = new HashSet<string>();
        private static readonly Dictionary<ServerClientTypePair, VssHttpClientBase> _tfsClients = new Dictionary<ServerClientTypePair, VssHttpClientBase>();
    }

    internal class ServerClientTypePair
    {
        public string ServerName { get; set; }

        public Type VssHttpClient { get; set; }

        public override bool Equals(object obj)
        {
            return obj is ServerClientTypePair other
                   && ServerName == other.ServerName
                   && VssHttpClient == other.VssHttpClient;

        }

        protected bool Equals(ServerClientTypePair other)
        {
            return ServerName == other.ServerName
                   && VssHttpClient == other.VssHttpClient;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ServerName != null ? ServerName.GetHashCode() : 0) * 397) ^ (VssHttpClient != null ? VssHttpClient.GetHashCode() : 0);
            }
        }
    }
}
