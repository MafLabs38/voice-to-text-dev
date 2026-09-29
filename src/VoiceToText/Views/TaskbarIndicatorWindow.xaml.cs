using System.Windows;
using System.Windows.Threading;

namespace VoiceToText.Views;

/// <summary>
/// Fenêtre technique, sans contenu visible : sa seule raison d'être est de fournir un bouton
/// dans la barre des tâches (donc visible sur tous les écrans si l'option Windows correspondante
/// est activée) sur lequel <see cref="Services.TaskbarBadgeService"/> peut superposer une pastille
/// d'enregistrement. Reste minimisée en permanence ; un clic dessus (restauration) déclenche
/// l'action fournie puis se re-minimise aussitôt, plutôt que d'exposer une fenêtre vide.
/// </summary>
public partial class TaskbarIndicatorWindow : Window
{
    private bool _allowClose;
    private DateTime _lastHandledActivation = DateTime.MinValue;

    public TaskbarIndicatorWindow(Action onActivatedRequest)
    {
        InitializeComponent();

        // Minimiser directement dans le XAML (avant le premier Show()) est un piège WPF connu :
        // Windows 11 ne matérialise alors jamais correctement la fenêtre, et un clic sur son
        // bouton de barre des tâches affiche une carte de prévisualisation cassée au lieu de
        // simplement la restaurer. On la montre normalement puis on la minimise une fois chargée.
        Loaded += (_, _) => WindowState = WindowState.Minimized;

        Activated += (_, _) =>
        {
            // Un vrai clic déclenche la restauration native de Windows (SC_RESTORE) ET notre
            // Activated quasi simultanément. Si on re-minimise ici de façon synchrone, on entre
            // en course avec la transition native de Windows : celle-ci peut se terminer APRÈS
            // notre re-minimize et repasser la fenêtre à Normal, ce qui redéclenche Activated —
            // et donc l'action — plusieurs fois pour un seul clic (bascules en cascade, état final
            // imprévisible). Deux garde-fous : on reporte le re-minimize après que la transition
            // native se soit calmée (BeginInvoke à priorité Input, pas immédiat), et on ignore
            // toute réactivation en cascade survenant dans la foulée d'une action déjà traitée.
            var now = DateTime.UtcNow;
            if (now - _lastHandledActivation < TimeSpan.FromMilliseconds(400))
            {
                return;
            }

            _lastHandledActivation = now;
            onActivatedRequest();
            Dispatcher.BeginInvoke(new Action(() => WindowState = WindowState.Minimized), DispatcherPriority.Input);
        };

        Closing += (_, e) =>
        {
            if (_allowClose)
            {
                return;
            }

            // Cette fenêtre doit rester en permanence dans la barre des tâches : on empêche sa
            // fermeture accidentelle (Alt+F4 pendant qu'elle est restaurée) et on la re-minimise.
            e.Cancel = true;
            WindowState = WindowState.Minimized;
        };
    }

    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }
}
