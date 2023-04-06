using System;
using System.IO;
using LiteDB;

namespace MergeTracker
{
    public static class DatabaseEngine
    {
        public static LiteDatabase DatabaseInstance
        {
            get
            {
                return _databaseInstance ??= new Func<LiteDatabase>(() =>
                {
                    if (!Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), APPDATA_FOLDER_NAME)))
                    {
                        Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), APPDATA_FOLDER_NAME));
                    }

                    LiteDatabase database = new LiteDatabase(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), APPDATA_FOLDER_NAME, CONFIG_FILE_NAME));

                    // Perform migrations
                    if (database.UserVersion == 0)
                    {
                        ILiteCollection<BsonDocument> mergeTargets = database.GetCollection("mergetarget");
                        foreach (BsonDocument mergeTarget in mergeTargets.FindAll())
                        {
                            mergeTarget["WorkItemId"] = mergeTarget["BugNumber"];
                            mergeTarget["ChangesetId"] = mergeTarget["Changeset"];
                            mergeTargets.Update(mergeTarget);
                        }

                        ILiteCollection<BsonDocument> rootConfigurations = database.GetCollection("rootconfiguration");
                        foreach (BsonDocument rootConfiguration in rootConfigurations.FindAll())
                        {
                            rootConfiguration["ShowProjectSettings"] = rootConfiguration["ShowTfsSettings"];
                            rootConfiguration["OnPremTfsUsername"] = rootConfiguration["TfsUsername"];
                            rootConfiguration["OnPremTfsPassword"] = rootConfiguration["TfsPassword"];
                            rootConfiguration["CloudAzureDevOpsToken"] = rootConfiguration["TfsToken"];
                            rootConfigurations.Update(rootConfiguration);
                        }

                        database.UserVersion = 1;
                    }

                    return database;
                })();
            }
        }
        private static LiteDatabase _databaseInstance;

        public static void Shutdown()
        {
            _rootConfigurationCollection = null;
            _mergeItemCollection = null;
            _mergeTargetCollection = null;
            DatabaseInstance?.Dispose();
            _databaseInstance = null;
        }

        public static ILiteCollection<RootConfiguration> RootConfigurationCollection => _rootConfigurationCollection ??= DatabaseInstance.GetCollection<RootConfiguration>("rootconfiguration");
        private static ILiteCollection<RootConfiguration> _rootConfigurationCollection;

        public static ILiteCollection<MergeItem> MergeItemCollection => _mergeItemCollection ??= DatabaseInstance.GetCollection<MergeItem>("mergeitem");
        private static ILiteCollection<MergeItem> _mergeItemCollection;

        public static ILiteCollection<MergeTarget> MergeTargetCollection => _mergeTargetCollection ??= DatabaseInstance.GetCollection<MergeTarget>("mergetarget");
        private static ILiteCollection<MergeTarget> _mergeTargetCollection;

        private const string CONFIG_FILE_NAME = "MergeTracker.db";

#if DEBUG
        private const string APPDATA_FOLDER_NAME = "MergeTracker_Debug";
#else
        private const string APPDATA_FOLDER_NAME = "MergeTracker";
#endif
    }
}
