using System.Windows.Forms;

namespace VoiceToText.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Drawing.Icon _idleIcon;
    private readonly System.Drawing.Icon _activeIcon;
    private readonly ToolStripMenuItem _updateMenuItem;
    private readonly ToolStripMenuItem _secondaryTriggerMenuItem;
    private readonly ToolStripMenuItem _overlayMenuItem;
    private readonly System.Drawing.Bitmap _greenDot = StatusDot.CreateBitmap(StatusDot.Green);
    private readonly System.Drawing.Bitmap _redDot = StatusDot.CreateBitmap(StatusDot.Red);

    /// <summary>Fournie par l'appli : l'overlay est-il actuellement affiché ? Lu à chaque ouverture du menu pour la pastille.</summary>
    public Func<bool>? IsOverlayVisible { get; set; }

    public event EventHandler? ToggleOverlayRequested;
    public event EventHandler? ToggleSecondaryTriggerRequested;
    public event EventHandler? OpenSettingsRequested;
    public event EventHandler? ShowLastTranscriptionRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? UpdateRequested;

    public TrayIconService()
    {
        (_idleIcon, _activeIcon) = TrayIconFactory.CreateTrayIcons();

        _updateMenuItem = new ToolStripMenuItem("Mise à jour disponible…", null, (_, _) => UpdateRequested?.Invoke(this, EventArgs.Empty))
        {
            Visible = false,
        };

        // L'état est piloté par l'appli (SetSecondaryTriggerState) pour rester cohérent avec les
        // Paramètres et la Jump List ; il est montré par une pastille verte (ON) ou rouge (OFF).
        _secondaryTriggerMenuItem = new ToolStripMenuItem("Déclencheur secondaire", _redDot,
            (_, _) => ToggleSecondaryTriggerRequested?.Invoke(this, EventArgs.Empty));

        _overlayMenuItem = new ToolStripMenuItem("Afficher/masquer l'overlay", _greenDot,
            (_, _) => ToggleOverlayRequested?.Invoke(this, EventArgs.Empty));

        var menu = new ContextMenuStrip();
        // L'overlay peut être affiché/masqué par d'autres voies (raccourci, bouton, Jump List) :
        // on relit son état à chaque ouverture du menu plutôt que de le suivre événement par événement.
        menu.Opening += (_, _) => _overlayMenuItem.Image = IsOverlayVisible?.Invoke() == false ? _redDot : _greenDot;
        menu.Items.Add(_overlayMenuItem);
        menu.Items.Add(_secondaryTriggerMenuItem);
        menu.Items.Add("Paramètres…", null, (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Dernière transcription", null, (_, _) => ShowLastTranscriptionRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_updateMenuItem);
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

    /// <summary>Met à jour la case cochée et le libellé de l'entrée du déclencheur secondaire.</summary>
    public void SetSecondaryTriggerState(bool enabled, string triggerDescription)
    {
        _secondaryTriggerMenuItem.Image = enabled ? _greenDot : _redDot;
        _secondaryTriggerMenuItem.Text = $"Déclencheur secondaire : {(enabled ? "ON" : "OFF")}";
        _secondaryTriggerMenuItem.ToolTipText = triggerDescription;
    }

    /// <summary>Bulle de notification brève (retour visuel quand le basculement vient de la Jump List, sans case à cocher visible).</summary>
    public void ShowBalloon(string text)
    {
        _notifyIcon.ShowBalloonTip(2000, "Dictée universelle", text, ToolTipIcon.Info);
    }

    /// <summary>Affiche/masque l'entrée « Mise à jour disponible » du menu, avec le numéro de version.</summary>
    public void SetUpdateAvailable(string? version)
    {
        if (string.IsNullOrEmpty(version))
        {
            _updateMenuItem.Visible = false;
            return;
        }

        _updateMenuItem.Text = $"Mise à jour disponible (v{version})…";
        _updateMenuItem.Visible = true;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _greenDot.Dispose();
        _redDot.Dispose();
        _idleIcon.Dispose();
        _activeIcon.Dispose();
    }
}
