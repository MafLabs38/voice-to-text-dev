using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using VoiceToText.Models;

namespace VoiceToText.Services;

/// <summary>
/// Pastille superposée sur le bouton de barre des tâches (façon Teams/Discord « en appel »),
/// via l'API COM native ITaskbarList3 — c'est le seul mécanisme Windows pour ça. Nécessite une
/// fenêtre visible dans la barre des tâches (voir Views/TaskbarIndicatorWindow) et, pour être
/// visible sur plusieurs écrans, le réglage Windows « Afficher la barre des tâches sur tous les
/// écrans » (Paramètres > Personnalisation > Barre des tâches > Comportements de la barre des
/// tâches > « Afficher ma barre des tâches sur tous les affichages »).
/// </summary>
public sealed class TaskbarBadgeService : IDisposable
{
    [ComImport]
    [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    private class TaskbarInstance
    {
    }

    // Vtable ITaskbarList/ITaskbarList2/ITaskbarList3, dans cet ordre exact : c'est une interface
    // COM classique (vtable), donc toute méthode manquante ou mal ordonnée ici désaligne l'appel
    // de SetOverlayIcon (et de tout ce qui suit) même si ces méthodes-là ne sont jamais utilisées.
    [ComImport]
    [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        // ITaskbarList
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);

        // ITaskbarList2
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

        // ITaskbarList3
        void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
        void SetProgressState(IntPtr hwnd, int tbpFlags);
        void RegisterTab(IntPtr hwndTab, IntPtr hwndMDI);
        void UnregisterTab(IntPtr hwndTab);
        void SetTabOrder(IntPtr hwndTab, IntPtr hwndInsertBefore);
        void SetTabActive(IntPtr hwndTab, IntPtr hwndMDI, uint tbatFlags);
        void ThumbBarAddButtons(IntPtr hwnd, uint cButtons, IntPtr pButtons);
        void ThumbBarUpdateButtons(IntPtr hwnd, uint cButtons, IntPtr pButtons);
        void ThumbBarSetImageList(IntPtr hwnd, IntPtr himl);
        void SetOverlayIcon(IntPtr hwnd, IntPtr hIcon, [MarshalAs(UnmanagedType.LPWStr)] string? pszDescription);
        void SetThumbnailTooltip(IntPtr hwnd, [MarshalAs(UnmanagedType.LPWStr)] string? pszTip);
        void SetThumbnailClip(IntPtr hwnd, IntPtr prcClip);
    }

    private readonly ITaskbarList3 _taskbarList = (ITaskbarList3)new TaskbarInstance();
    private readonly IntPtr _hwnd;
    private readonly DispatcherTimer _animationTimer;
    private readonly IntPtr[] _recordingFrames;
    private readonly IntPtr[] _processingFrames;
    private IntPtr[]? _activeFrames;
    private string? _activeDescription;
    private int _frameIndex;

    public TaskbarBadgeService(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _taskbarList.HrInit();
        _recordingFrames = BuildPulseFrames();
        _processingFrames = BuildDotsFrames();

        _animationTimer = new DispatcherTimer();
        _animationTimer.Tick += (_, _) =>
        {
            if (_activeFrames is null)
            {
                return;
            }

            _frameIndex = (_frameIndex + 1) % _activeFrames.Length;
            _taskbarList.SetOverlayIcon(_hwnd, _activeFrames[_frameIndex], _activeDescription);
        };
    }

    /// <summary>
    /// Reflète l'état de la dictée sur le bouton de barre des tâches : un point rouge qui pulse
    /// pendant l'enregistrement (même langage visuel que le voyant REC de l'overlay), puis bascule
    /// *immédiatement* vers un indicateur distinct (trois points) pendant la transcription/le
    /// collage — sans ce changement immédiat, l'utilisateur qui reclique pour arrêter ne voit
    /// aucun retour visuel pendant l'arrêt réel du flux audio et croit que son clic n'a pas pris.
    /// </summary>
    public void SetState(DictationState state)
    {
        switch (state)
        {
            case DictationState.Recording:
                StartAnimation(_recordingFrames, TimeSpan.FromMilliseconds(120), "Enregistrement en cours");
                break;
            case DictationState.Transcribing:
            case DictationState.Pasting:
                StartAnimation(_processingFrames, TimeSpan.FromMilliseconds(350), "Transcription en cours");
                break;
            default:
                StopAnimation();
                break;
        }
    }

    private void StartAnimation(IntPtr[] frames, TimeSpan interval, string description)
    {
        if (ReferenceEquals(_activeFrames, frames) && _animationTimer.IsEnabled)
        {
            return;
        }

        _activeFrames = frames;
        _activeDescription = description;
        _frameIndex = 0;
        _animationTimer.Interval = interval;
        _taskbarList.SetOverlayIcon(_hwnd, frames[0], description);
        _animationTimer.Start();
    }

    private void StopAnimation()
    {
        if (_activeFrames is null && !_animationTimer.IsEnabled)
        {
            return;
        }

        _animationTimer.Stop();
        _activeFrames = null;
        _activeDescription = null;
        _taskbarList.SetOverlayIcon(_hwnd, IntPtr.Zero, null);
    }

    private static IntPtr[] BuildPulseFrames()
    {
        // Un vrai clignotement (allumé/éteint net), pas un fondu progressif — un fondu ne se lit
        // pas comme un voyant REC. Un petit cadre sombre encadre le point rouge en permanence pour
        // bien le lire comme un indicateur d'enregistrement plutôt qu'une simple pastille ambiguë.
        bool[] lit = { true, true, true, false, false, false };
        var frames = new IntPtr[lit.Length];
        for (var i = 0; i < lit.Length; i++)
        {
            using var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var frameBrush = new SolidBrush(Color.FromArgb(255, 24, 24, 24));
                g.FillRectangle(frameBrush, 0, 0, 16, 16);

                if (lit[i])
                {
                    using var dotBrush = new SolidBrush(Color.FromArgb(255, 255, 0, 0));
                    g.FillEllipse(dotBrush, 4, 4, 8, 8);
                }
            }

            frames[i] = bitmap.GetHicon();
        }

        return frames;
    }

    /// <summary>Trois points qui s'allument un à un puis se réinitialisent (façon « en train d'écrire… »).</summary>
    private static IntPtr[] BuildDotsFrames()
    {
        bool[][] litPattern =
        {
            new[] { true, false, false },
            new[] { true, true, false },
            new[] { true, true, true },
            new[] { false, false, false },
        };

        var frames = new IntPtr[litPattern.Length];
        for (var i = 0; i < litPattern.Length; i++)
        {
            using var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var lit = new SolidBrush(Color.FromArgb(255, 59, 130, 246));
                using var unlit = new SolidBrush(Color.FromArgb(90, 59, 130, 246));
                for (var dot = 0; dot < 3; dot++)
                {
                    var brush = litPattern[i][dot] ? lit : unlit;
                    g.FillEllipse(brush, 1 + dot * 5, 6, 4, 4);
                }
            }

            frames[i] = bitmap.GetHicon();
        }

        return frames;
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public void Dispose()
    {
        _animationTimer.Stop();
        foreach (var frame in _recordingFrames.Concat(_processingFrames))
        {
            DestroyIcon(frame);
        }
    }
}
