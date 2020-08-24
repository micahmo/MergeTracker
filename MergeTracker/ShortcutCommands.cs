using System.Windows.Input;

namespace MergeTracker
{
    public static class ShortcutCommands
    {
        static ShortcutCommands()
        {
            ReloadCommand.InputGestures.Add(new KeyGesture(Key.F5));
        }

        public static RoutedCommand ReloadCommand { get; } = new RoutedCommand();
    }
}
