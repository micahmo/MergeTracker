using System;
using System.Windows.Input;
using System.Windows.Threading;

namespace MergeTracker
{
    public class WaitCursor : IDisposable
    {
        public WaitCursor(Cursor cursor, DispatcherPriority dispatcherPriority = DispatcherPriority.Normal)
        {
            Mouse.OverrideCursor = cursor;
            _dispatcherPriority = dispatcherPriority;
        }

        public void Dispose()
        {
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = null;
            }, _dispatcherPriority);
        }

        private readonly DispatcherPriority _dispatcherPriority;
    }
}
