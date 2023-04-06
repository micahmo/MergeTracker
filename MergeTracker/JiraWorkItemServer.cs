using System.Diagnostics;
using Atlassian.Jira;
using System.Threading.Tasks;

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
            Issue workItem = await GetWorkItem(workItemId);
            return $"{workItem.Jira.Url}browse/{workItem.Key}";
        }

        public async Task<string> GetWorkItemTitle(string workItemId)
        {
            return (await GetWorkItem(workItemId)).Summary;
        }

        private Task<Issue> GetWorkItem(string workItemId)
        {
            return GetClient().Issues.GetIssueAsync(workItemId);
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
    }
}
