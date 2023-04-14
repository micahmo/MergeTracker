using System;
using System.IO;
using System.Windows;
using Jot;
using Jot.Storage;

namespace MergeTracker
{
    /// <summary>
    /// Defines application-wide settings which will be persisted across sessions
    /// </summary>
    internal class AppSettings
    {
        #region Singleton member

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static AppSettings Instance { get; } = new AppSettings();

        #endregion

        #region Private constructor

        /// <summary>
        /// Constructor
        /// </summary>
        private AppSettings()
        {
            // Set up Window tracking
            Tracker.Configure<Window>()
                .Id(w => w.Name, new Size(SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight))
                .Properties(w => new { w.Top, w.Width, w.Height, w.Left, w.WindowState })
                .PersistOn(nameof(Window.Closing))
                .StopTrackingOn(nameof(Window.Closing))
                .WhenPersistingProperty((w, p) => p.Cancel = p.Property == nameof(w.WindowState) && w.WindowState == WindowState.Minimized);
        }

        #endregion

        #region Statics

#if DEBUG
        public string AppDataFolderName = "MergeTracker_Debug";
#else
        public string AppDataFolderName = "MergeTracker";
#endif

        #endregion

        #region Public properties

        /// <summary>
        /// The public tracker instance. Can be used to track things other than the <see cref="Instance"/>.
        /// </summary>
        public Tracker Tracker => _tracker ??= new Tracker(new JsonFileStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName)));
        private Tracker _tracker;

        #endregion
    }
}