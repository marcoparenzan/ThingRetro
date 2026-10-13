using System.Collections.Concurrent;

namespace Fake64.C64;

// Commands sent by other threads (UI, program loading, cartridges) and run by the emulation thread
public partial class Board
{
    readonly ConcurrentQueue<Action> pendingCommands = new();

    // Runs an action on the emulation thread between two cycles, so it does not read or write memory
    // while the CPU is running; if the real-time clock is not active it runs it at once
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
