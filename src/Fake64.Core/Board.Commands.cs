using System.Collections.Concurrent;

namespace Fake64;

// Comandi inviati da altri thread (UI, caricamento di programmi, cartucce) ed eseguiti dal thread di emulazione
public partial class Board
{
    readonly ConcurrentQueue<Action> pendingCommands = new();

    // Esegue un'azione sul thread di emulazione fra due cicli, così non legge o scrive la memoria
    // mentre la CPU sta eseguendo; se il clock in tempo reale non è attivo la esegue subito
    public void Enqueue(Action command)
    {
        if (clock is null) command();
        else pendingCommands.Enqueue(command);
    }

    void RunPendingCommands()
    {
        while (pendingCommands.TryDequeue(out var command)) command();
    }
}
