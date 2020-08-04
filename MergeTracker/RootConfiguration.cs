using System.Collections.ObjectModel;
using System.IO;
using GalaSoft.MvvmLight;
using SpecialFolder = System.Environment.SpecialFolder;

namespace MergeTracker
{
    public class RootConfiguration : ObservableObject
    {
        public static RootConfiguration Instance { get; private set; }

        public ObservableCollection<MergeItem> MergeItems { get; } = new ObservableCollection<MergeItem>();

        public string BugNumberFilter
        {
            get => _bugNumberFilter;
            set => Set(nameof(BugNumberFilter), ref _bugNumberFilter, value);
        }
        private string _bugNumberFilter;

        public string ChangesetNumberFilter
        {
            get => _changesetNumberFilter;
            set => Set(nameof(ChangesetNumberFilter), ref _changesetNumberFilter, value);
        }
        private string _changesetNumberFilter;

        public string TargetBranchFilter
        {
            get => _targetBranchFilter;
            set => Set(nameof(TargetBranchFilter), ref _targetBranchFilter, value);
        }
        private string _targetBranchFilter;

        public bool NotCompletedFilter
        {
            get => _notCompletedFilter;
            set => Set(nameof(NotCompletedFilter), ref _notCompletedFilter, value);
        }
        private bool _notCompletedFilter;

        public bool UseTfs
        {
            get => _useTfs;
            set => Set(nameof(UseTfs), ref _useTfs, value);
        }
        private bool _useTfs;

        public string TfsUsername
        {
            get => _tfsUsername;
            set => Set(nameof(TfsUsername), ref _tfsUsername, value);
        }
        private string _tfsUsername;

        public string TfsPassword
        {
            get => _tfsPassword;
            set => Set(nameof(TfsPassword), ref _tfsPassword, value);
        }
        private string _tfsPassword;

        public string TfsPath
        {
            get => _tfsPath;
            set => Set(nameof(TfsPath), ref _tfsPath, value);
        }
        private string _tfsPath;

        public static RootConfiguration Load()
        {
            RootConfiguration result = null;

            bool fileExistedBeforeFirstRead = File.Exists(JsonSerialization.GetCustomConfigFilePath(SpecialFolder.ApplicationData, CONFIG_FILE_NAME, createIfNotExists: false));

            try
            {
                result = JsonSerialization.DeserializeObjectFromCustomConfigFile<RootConfiguration>(CONFIG_FILE_NAME, SpecialFolder.ApplicationData);
            }
            catch
            {
                // Intentionally empty
            }

            if (result is { })
            {
                return Instance = result;
            }
            else
            {
                if (fileExistedBeforeFirstRead)
                {
                    // The file exists, but there was a problem deserializing it.
                    // Be sure to back up the existing file before overwriting it with an empty instance.
                    File.Copy(JsonSerialization.GetCustomConfigFilePath(SpecialFolder.ApplicationData, CONFIG_FILE_NAME),
                              JsonSerialization.GetCustomConfigFilePath(SpecialFolder.ApplicationData, CONFIG_BACKUP_NAME), overwrite: true);
                }

                Save(new RootConfiguration());
                return Instance = Load();
            }
        }

        public static void Save(RootConfiguration rootConfiguration)
        {
            JsonSerialization.SerializeObjectToCustomConfigFile(CONFIG_FILE_NAME, rootConfiguration, SpecialFolder.ApplicationData);
        }

        private const string CONFIG_FILE_NAME = "MergeTracker.config";

        private const string CONFIG_BACKUP_NAME = "MergeTracker.config.bak";
    }
}
