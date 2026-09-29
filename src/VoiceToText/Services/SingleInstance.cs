using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace VoiceToText.Services;

/// <summary>
/// Garantit une seule instance de l'appli à la fois, et transmet à cette instance les actions
/// demandées par une seconde invocation (ex. un item de Jump List de la barre des tâches, qui
/// relance toujours l'exe comme un nouveau processus avec des arguments plutôt que d'appeler du
/// code dans le processus déjà en cours — c'est la seule façon dont Windows permet à une appli de
/// personnaliser le clic droit sur son propre bouton de barre des tâches).
/// </summary>
public static class SingleInstance
{
    private const string MutexName = "VoiceToTextDictation.SingleInstance";
    private const string PipeName = "VoiceToTextDictation.Ipc";

    private static Mutex? _mutex;

    /// <summary>Émis (sur un thread d'arrière-plan) quand une seconde instance transmet une action.</summary>
    public static event Action<string>? ActionReceived;

    /// <summary>
    /// À appeler tout au début de Main(). Retourne true si cette instance doit démarrer
    /// normalement (première instance), false si une autre instance tourne déjà — auquel cas
    /// l'action éventuellement demandée (args[0]) lui a été transmise et ce processus doit
    /// s'arrêter immédiatement sans rien initialiser (pas de tray, pas de fenêtre, pas de hotkeys).
    /// </summary>
    public static bool TryAcquire(string[] args)
    {
        _mutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out var isFirstInstance);
        if (isFirstInstance)
        {
            return true;
        }

        var action = args.Length > 0 ? args[0] : null;
        if (!string.IsNullOrEmpty(action))
        {
            TrySend(action);
        }

        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    /// <summary>À appeler par la première instance une fois démarrée : écoute en arrière-plan les actions transmises par de futures secondes instances.</summary>
    public static void StartListening()
    {
        var thread = new Thread(ListenLoop) { IsBackground = true, Name = "VoiceToText IPC listener" };
        thread.Start();
    }

    private static void ListenLoop()
    {
        while (true)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                server.WaitForConnection();
                using var reader = new StreamReader(server);
                var action = reader.ReadLine();
                if (!string.IsNullOrEmpty(action))
                {
                    ActionReceived?.Invoke(action);
                }
            }
            catch
            {
                // Best-effort : une connexion ratée ne doit pas arrêter l'écoute pour les suivantes.
            }
        }
    }

    private static void TrySend(string action)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1000);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(action);
        }
        catch
        {
            // Best-effort : si l'instance principale ne répond pas, on abandonne silencieusement.
        }
    }
}
