using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;

namespace MergeTracker
{
    /// <summary>
    /// Defines a custom DataGrid that tabs to the next TabStop control when Enter is pressed
    /// </summary>
    public class MyDataGrid : DataGrid
    {

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Enter && Keyboard.PrimaryDevice.ActiveSource is { })
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) == false)
                {
                    // Standard Enter key. Simulate the Tab press to focus the control.
                    InputManager.Current.ProcessInput(new KeyEventArgs(Keyboard.PrimaryDevice, Keyboard.PrimaryDevice.ActiveSource, 0, Key.Tab)
                    {
                        RoutedEvent = Keyboard.KeyDownEvent
                    });
                }
                else
                {
                    // This is a Shift-Enter. We have to wait for the keys to be processed, so that the Shift modifier key is no longer pressed.
                    // Queue up the simulated Tab event for when the Shift key is raised.
                    Key shiftKey = Keyboard.IsKeyDown(Key.LeftShift) ? Key.LeftShift : Key.RightShift;
                    _onKeyUpActions[shiftKey] = new KeyEventArgs(Keyboard.PrimaryDevice, Keyboard.PrimaryDevice.ActiveSource, 0, Key.Tab)
                    {
                        RoutedEvent = Keyboard.KeyDownEvent
                    };
                }
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);

            if (_onKeyUpActions.TryGetValue(e.Key, out var action))
            {
                InputManager.Current.ProcessInput(action);
                
                _onKeyUpActions.Remove(e.Key);
            }
        }

        private readonly Dictionary<Key, KeyEventArgs> _onKeyUpActions = new Dictionary<Key, KeyEventArgs>();
    }
}