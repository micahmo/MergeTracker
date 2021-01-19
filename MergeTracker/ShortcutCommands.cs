using System.Windows.Input;

namespace MergeTracker
{
    public static class ShortcutCommands
    {
        static ShortcutCommands()
        {
            ReloadCommand.InputGestures.Add(new KeyGesture(Key.F5));
            GoToItemCommand.InputGestures.Add(new KeyGesture(Key.G, ModifierKeys.Control));
            FindCommand.InputGestures.Add(new KeyGesture(Key.F, ModifierKeys.Control));
            AboutBoxCommand.InputGestures.Add(new KeyGesture(Key.F1));
        }

        public static RoutedCommand ReloadCommand { get; } = new RoutedCommand();

        public static RoutedCommand GoToItemCommand { get; } = new RoutedCommand();

        public static RoutedCommand FindCommand { get; } = new RoutedCommand();

        public static RoutedCommand AboutBoxCommand { get; } = new RoutedCommand();
    }
}
