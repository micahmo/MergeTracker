using System.Diagnostics;
using Atlassian.Jira;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using Newtonsoft.Json;

namespace MergeTracker
{
    public class JiraWorkItemServer : IWorkItemServer
    {
        public string ServerName { get; set; }

        public async Task OpenWorkItem(string workItemId)
        {
            Process.Start(await GetWorkItemUrl(workItemId));
        }

        public async Task<string> GetWorkItemUrl(string workItemId)
        {
            JiraIssue workItem = await GetWorkItem(workItemId);
            return ServerName
                .AppendPathSegment("browse")
                .AppendPathSegment(workItem.Key);
        }

        public async Task<string> GetWorkItemTitle(string workItemId)
        {
            return (await GetWorkItem(workItemId)).Summary;
        }

        private async Task<JiraIssue> GetWorkItem(string workItemId)
        {
            string issue = await ServerName
                .AppendPathSegment("rest")
                .AppendPathSegment("api")
                .AppendPathSegment("2")
                .AppendPathSegment("issue")
                .AppendPathSegment(workItemId)
                .WithHeader("Authorization", $"Basic {RootConfiguration.Instance.JiraPassword}")
                .GetStringAsync();

            return JsonConvert.DeserializeObject<JiraIssue>(issue);
        }

        private Jira GetClient()
        {
            if (_jiraClient != null && _password == RootConfiguration.Instance.JiraPassword)
            {
                return _jiraClient;
            }

            return _jiraClient = Jira.CreateRestClient(ServerName, RootConfiguration.Instance.JiraUsername, _password = RootConfiguration.Instance.JiraPassword);
        }
        private Jira _jiraClient;
        private string _password;

        private class JiraIssue
        {
            public string Key { get; set; }

            public string Summary { get; set; }
        }
    }
}
