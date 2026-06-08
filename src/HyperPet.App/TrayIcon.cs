using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HyperPet.App;

/// <summary>
/// System-tray icon shown while HyperPet runs. Right-click menu:
/// Come, Settings, Check for update, Tuck Away. Double-click opens Settings.
/// Dispose on app exit to avoid a ghost icon.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayIcon(
        string tooltip,
        Action onCome,
        Action onSettings,
        Action onCheckForUpdate,
        Action onTuckAway)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Come", null, (_, _) => onCome());
        menu.Items.Add("Settings", null, (_, _) => onSettings());
        menu.Items.Add("Check for update", null, (_, _) => onCheckForUpdate());
        menu.Items.Add("Tuck Away", null, (_, _) => onTuckAway());

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = Truncate(tooltip, 63), // NotifyIcon.Text max length is 63
            Visible = true,
            ContextMenuStrip = menu,
        };

        _icon.DoubleClick += (_, _) => onSettings();
    }

    /// <summary>Show a tray balloon (used for update-check outcomes).</summary>
    public void Notify(string message)
    {
        _icon.BalloonTipTitle = "HyperPet";
        _icon.BalloonTipText = message;
        _icon.ShowBalloonTip(4000);
    }

    private static Icon LoadIcon()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "HyperPet.ico");
        if (File.Exists(path))
        {
            return new Icon(path);
        }

        return SystemIcons.Application;
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
