using System.Windows.Input;

namespace HyperPet.App.Views;

internal static class DebugModeShortcut
{
    public const string GestureText = "Ctrl+Alt+D";

    public static bool TryHandle(KeyEventArgs args, Action toggle)
    {
        if (args.Handled) return false;
        var key = args.Key == Key.System ? args.SystemKey : args.Key;
        if (!TryHandle(key, Keyboard.Modifiers, args.IsRepeat, toggle)) return false;
        args.Handled = true;
        return true;
    }

    internal static bool TryHandle(Key key, ModifierKeys modifiers, bool repeat, Action toggle)
    {
        if (key != Key.D || modifiers != (ModifierKeys.Control | ModifierKeys.Alt)) return false;
        if (!repeat) toggle();
        return true;
    }
}
