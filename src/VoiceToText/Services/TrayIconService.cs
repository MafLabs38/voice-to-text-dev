using System.Windows.Forms;

namespace VoiceToText.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Drawing.Icon _idleIcon;
    private readonly System.Drawing.Icon _activeIcon;

    public event EventHandler? ToggleOverlayRequested;
    public event EventHandler? OpenSettingsRequested;
    public event EventHandler? ShowLastTranscriptionRequested;
    public event EventHandler? ExitRequested;

    public TrayIconService()
    {
        (_idleIcon, _activeIcon) = TrayIconFactory.CreateTrayIcons();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Afficher/masquer l'overlay", null, (_, _) => ToggleOverlayRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Paramètres…", null, (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Dernière transcription", null, (_, _) => ShowLastTranscriptionRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new NotifyIcon
        {
            Icon = _idleIcon,
            Text = "Dictée universelle",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    public void SetActive(bool active)
    {
        _notifyIcon.Icon = active ? _activeIcon : _idleIcon;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _idleIcon.Dispose();
        _activeIcon.Dispose();
    }
}
