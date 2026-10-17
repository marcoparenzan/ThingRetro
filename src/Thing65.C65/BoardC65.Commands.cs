using System.Collections.Concurrent;

namespace Thing65.C65;

// Commands sent by other threads (UI, program loading) and run by the emulation thread; typing text on the keyboard
public partial class BoardC65
{
    readonly ConcurrentQueue<Action> pendingCommands = new();

    // Runs an action on the emulation thread between two cycles, so it does not read or write memory while the CPU
    // is running; if the real-time clock is not active it runs it at once
    public void Enqueue(Action command)
    {
        if (clock is null) command();
        else pendingCommands.Enqueue(command);
    }

    void RunPendingCommands()
    {
        while (pendingCommands.TryDequeue(out var command)) command();
    }

    // Emulates the given time as fast as possible, e.g. the boot before loading a program. Call it from the emulation
    // thread (inside Enqueue); real-time pacing resumes from the end of it
    public void FastForward(TimeSpan time)
    {
        RunCycles((long)(time.TotalSeconds * MASTER_CLOCK_HZ));
        resyncClock = true;
    }

    // ---- Typing -----------------------------------------------------------------------------------------
    readonly Queue<KeyTyper.Step> typing = new();
    int typingCyclesLeft;
    bool typingActive;

    // Types the text on the keyboard, as a person would, while the machine runs. Call it on the emulation thread
    public void TypeText(string text)
    {
        foreach (var step in KeyTyper.Steps(text)) typing.Enqueue(step);
        typingActive = typing.Count > 0;
    }

    // True while there is text left to type
    public bool IsTyping => typingActive;

    void AdvanceTyping()
    {
        if (typingCyclesLeft == 0)
        {
            if (typing.Count == 0)
            {
                typingActive = false;
                keyboard.ReleaseAll();
                return;
            }
            var step = typing.Dequeue();
            keyboard.SetPressedKeys(step.Keys);
            typingCyclesLeft = step.Cycles;
        }
        typingCyclesLeft--;
    }
}
