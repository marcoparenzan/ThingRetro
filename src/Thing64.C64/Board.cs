using System.Diagnostics;
using ThingRetroEmu.Chips;
using ThingRetroEmu.Buses;

namespace Thing64.C64;

// The C64 (PAL) motherboard: sockets for the MOS chips, RAM, color RAM, ROMs, PLA and the traces that
// connect everything. The chips do not know the board: the board wires them through their pinouts
public partial class Board
{
    Task? clock;

    readonly Keyboard keyboard = new();
    readonly Ram64KbChip ram64 = new();
    readonly ColorRamChip colorRam = new();

    // External sockets: video output (a monitor connects with a MonitorCable), audio output
    // (speakers, with an AudioCable), keyboard and the two control ports (joystick, paddles, mouse)
    public VideoPort VideoOut { get; } = new();
    public AudioPort AudioOut { get; } = new(MASTER_CLOCK_HZ);
    public Keyboard Keyboard => keyboard;
    public ControlPort ControlPort1 { get; } = new("control port 1");
    public ControlPort ControlPort2 { get; } = new("control port 2");
    public IecPort Iec { get; } = new();    // serial connector: a cable takes it to the disk drives

    // Called at the end of every cycle: for what is clocked with the computer and lives outside the board, such as the
    // disk drives on the IEC cable
    public Action? AfterCycle { get; set; }

    // Direct RAM access, bypassing the memory map (like a DMA): it can write under the ROMs too.
    // To be used from the emulation thread, for example through Enqueue
    public Ram64KbChip Ram => ram64;

    internal long TotalCycles => totalCycles;

    // Cycles with BA low: the VIC-II asked for the bus (43 per bad line; the CPU can still write in the first 3)
    internal long StolenCycles { get; private set; }

    // Cycle constants for PAL C64
    private const long MASTER_CLOCK_HZ = 985248; // PAL master clock frequency in Hz

    private long totalCycles;

    // The ROMs come from outside: the Board does not know where the files are (see SystemRoms).
    // The sockets are filled with the stock chips; they can be replaced (see Socket)
    public Board(SystemRoms roms, bool startClock = true)
    {
        ArgumentNullException.ThrowIfNull(roms);
        kernal = roms.Kernal;
        chargen = roms.Chargen;
        basic = roms.Basic;

        cpuBus = new CpuBus(this);
        vicBus = new VicBus(this);
        CreateSockets();

        CpuSocket.Insert(new MOS6510());
        VicSocket.Insert(new MOS6569());
        SidSocket.Insert(new MOS6581());
        Cia1Socket.Insert(new MOS6526());
        Cia2Socket.Insert(new MOS6526());

        Reset();

        if (startClock) Start();
    }

    // Starts the real-time emulation on a separate task
    public void Start()
    {
        if (clock is not null) return;

        // Dedicated thread: emulates the cycles needed to keep up with the clock, then sleeps 1 ms
        // instead of spinning (the loop used to keep a core busy even when the emulation was in step)
        var stop = clockStop = new CancellationTokenSource();
        clock = Task.Factory.StartNew(() =>
        {
            using var timerResolution = new TimerResolution(1);
            var sw = Stopwatch.StartNew();
            long startCycles = totalCycles;

            while (!stop.IsCancellationRequested)
            {
                // Commands from other threads (e.g. loading a .prg from the UI)
                RunPendingCommands();

                // After a fast-forward the clock starts again from here, instead of waiting for real time to catch up
                if (resyncClock)
                {
                    resyncClock = false;
                    sw.Restart();
                    startCycles = totalCycles;
                }

                // Converts the elapsed time into PAL cycles without losing precision
                long targetCycles = startCycles + (long)(sw.ElapsedTicks * (double)MASTER_CLOCK_HZ / Stopwatch.Frequency);

                if (totalCycles >= targetCycles)
                {
                    Thread.Sleep(1);
                    continue;
                }

                while (totalCycles < targetCycles)
                    ExecuteCycle();
            }
        }, TaskCreationOptions.LongRunning);
    }

    CancellationTokenSource? clockStop;
    bool resyncClock;

    // Stops the real-time emulation and waits for the emulation thread to finish (the machine keeps its state:
    // Start resumes it). Commands still queued are dropped
    public void Stop()
    {
        if (clock is null) return;
        clockStop!.Cancel();
        clock.Wait();
        clock = null;
        clockStop.Dispose();
        clockStop = null;
        pendingCommands.Clear();
    }

    // Emulates the given time as fast as possible, e.g. the KERNAL boot before loading a program.
    // Call it from the emulation thread (inside Enqueue); real-time pacing resumes from the end of it
    public void FastForward(TimeSpan time)
    {
        RunCycles((long)(time.TotalSeconds * MASTER_CLOCK_HZ));
        resyncClock = true;
    }

    // Runs a fixed number of cycles synchronously, without the real-time clock (startClock: false): used by the tests
    // and by hosts that drive the board themselves, like the browser one frame at a time
    public void RunCycles(long cycles)
    {
        for (long i = 0; i < cycles; i++) ExecuteCycle();
    }

    // One clock cycle (φ2). The VIC-II's BA is read before advancing it, because it concerns the current
    // cycle; the VIC-II closes the cycle and, at the end of a line, draws it and handles the raster
    private void ExecuteCycle()
    {
        bool busAvailable = vic?.BusAvailable ?? true;

        cia1?.Tick();
        cia2?.Tick();
        sid?.Tick();
        AudioOut.Sample(sid?.AudioOutput ?? 0);
        cpu?.Tick(); // RDY and AEC are wired to the VIC-II: the CPU stops by itself when needed
        if (!busAvailable) StolenCycles++;
        vic?.Tick();

        totalCycles++;
        AfterCycle?.Invoke();
    }

    // End of a VIC-II frame: the picture goes to the video socket
    void OnFrameCompleted()
    {
        if (vic is not null) VideoOut.Present(vic);
    }

    // Power on / reset button: /RES reaches all the chips, the CPU restarts from the reset vector
    public void Reset()
    {
        ram64.Reset();
        colorRam.Reset();
        cia1?.Reset();
        cia2?.Reset();
        sid?.Reset();
        vic?.Reset();
        cartridge?.Reset();
        MemoryMap();
        cpu?.Reset(); // port $01 goes back to input: the map is updated before the vector is read
    }

    public byte Chargen(ushort addr) => chargen[addr];

    const ushort kernal_base = 0xE000;
    readonly byte[] kernal;
    const ushort chargen_base = 0xD000;
    readonly byte[] chargen;
    const ushort basic_base = 0xA000;
    readonly byte[] basic;
}
