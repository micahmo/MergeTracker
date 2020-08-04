using System;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace MergeTracker
{
    public static class TfsUtils
    {
        private static Task<bool> Initialize()
        {
            return Task.Run(() =>
            {
                bool result = false;

                if (_initializationState == InitializationState.Uninitialized)
                {
                    try
                    {
                        NetworkCredential nc = new NetworkCredential(RootConfiguration.Instance.TfsUsername, RootConfiguration.Instance.TfsPassword);
                        Uri uri = new Uri(RootConfiguration.Instance.TfsPath);

                        TfsClientCredentials tfsCredential = new TfsClientCredentials(new WindowsCredential(nc), false);

                        TfsTeamProjectCollection tpc = new TfsTeamProjectCollection(uri, tfsCredential);
                        tpc.Authenticate();

                        _workItemStore = tpc.GetService<WorkItemStore>();
                        _initializationState = InitializationState.SuccessfullyInitialized;
                        result = true;
                    }
                    catch (Exception ex)
                    {
                        _initializationState = InitializationState.FailedToInitialize;
                        MessageBox.Show($"There was an error contacting TFS server to retrieve bug title information.\n\n{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        result = false;
                    }
                }
                else if (_initializationState == InitializationState.SuccessfullyInitialized)
                {
                    result = true;
                }

                return result;
            });
        }

        public static Task<WorkItem> GetWorkItem(int workItemId)
        {
            return Task.Run(async () =>
            {
                if (await Initialize())
                {
                    return _workItemStore.GetWorkItem(workItemId);
                }

                return null;
            });
        }

        private static WorkItemStore _workItemStore;
        private static InitializationState _initializationState;

        private enum InitializationState
        {
            Uninitialized,
            SuccessfullyInitialized,
            FailedToInitialize
        }
    }
}
