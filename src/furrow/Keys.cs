using UnityEngine;

namespace Furrow
{
    /// <summary>
    /// Reading a configured key, the way the game reads one.
    ///
    /// This exists because of a bug that presents as "the key does nothing" while the
    /// config file holds exactly the right value, which is about the least helpful
    /// symptom available. Furrow's keys were polled with UnityEngine.Input.GetKeyDown,
    /// and Valheim runs on the new Input System: ZInput.GetKeyDown routes a KeyCode to
    /// Mouse.current, Keyboard.current or Gamepad.current by hand, and the legacy Input
    /// class does not see the middle mouse button here at all. The grid turn key was
    /// bound, correct, and unreachable.
    ///
    /// So: ZInput, which is the game's own reader and covers keyboard, mouse and pad in
    /// one call. It answers false rather than throwing before ZInput exists, which is
    /// the right answer that early anyway.
    /// </summary>
    internal static class Keys
    {
        /// <summary>
        /// True on the frame the key goes down, unless the keystroke belongs to a text
        /// field. Without that last part a key typed into chat or the console also
        /// drives the grid, which reads as the grid moving on its own.
        /// </summary>
        public static bool Pressed(KeyCode key)
        {
            if (key == KeyCode.None) return false;

            // logWarning: false - ZInput grumbles about KeyCodes it cannot map, and a
            // key nobody bound is a configuration choice rather than a fault.
            if (!ZInput.GetKeyDown(key, false)) return false;

            if (Chat.instance != null && Chat.instance.HasFocus()) return false;
            return !Console.IsVisible() && !TextInput.IsVisible();
        }
    }
}
