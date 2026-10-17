using System.Diagnostics;
using Thing65.Mos;

namespace Thing65.C65;

// The C65 motherboard: sockets for the chips (4510, VIC-III, two SIDs, two CIAs, DMAgic), 128 KB of RAM, the 128 KB
// system ROM and the traces that connect everything. Same principle as the C64 Board: the chips do not know the
// board, the board wires them through their pinouts.
// Time base: one cycle is 1 MHz (the PAL VIC-II line of 63 cycles); the FAST mode (3.5 MHz) is not modelled yet.
public partial class BoardC65
{
    Task? clock;
    CancellationTokenSource? clockStop;
    bool resyncClock;
    long totalCycles;

    // PAL VIC-II cycle rate: 63 cycles x 312 lines x 50 Hz
    const long MASTER_CLOCK_HZ = 985248;

    readonly Keyboard keyboard = new();
    readonly byte[] ram = new byte[0x20000];  // $00000-$1FFFF; the Color RAM is the last 2 KB, $1F800
    readonly byte[] rom;                      // $20000-$3FFFF
    readonly ParallelPort cpuPort = new();    // the 4510's port at $0000/$0001: memory map of the C64 mode
    readonly ParallelPort keyPort = new();    // port at $D607 (data) and $D608 (direction): bit 1 selects the keyboard's ninth line
    int fastCredit;                           // half cycles of the 3.5 MHz clock not yet spent

    // External sockets, as in the C64 board
    public VideoPort VideoOut { get; } = new();
    public AudioPort AudioOut { get; } = new(MASTER_CLOCK_HZ);
    public Keyboard Keyboard => keyboard;
    public ControlPort ControlPort1 { get; } = new("control port 1");
    public ControlPort ControlPort2 { get; } = new("control port 2");
    public IecPort Iec { get; } = new();                      // serial connector: a cable takes it to the disk drives

    // Called at the end of every board cycle (1 MHz of the PAL clock): for whatever is clocked with the computer and lives
    // outside the board, such as the disk drives on the IEC cable
    public Action? AfterCycle { get; set; }

    internal long TotalCycles => totalCycles;

    public BoardC65(SystemRom128 systemRom, bool startClock = true)
    {
        ArgumentNullException.ThrowIfNull(systemRom);
        rom = systemRom.Image;

        cpuBus = new CpuBus(this);
        vicBus = new VicBus(this);
        dmaBus = new DmaBus(this);
        CreateSockets();

        CpuSocket.Insert(new MOS4510());
        VicSocket.Insert(new CSG4567());
        SidRightSocket.Insert(new MOS6581());
        SidLeftSocket.Insert(new MOS6581());
        Cia1Socket.Insert(new MOS6526());
        Cia2Socket.Insert(new MOS6526());
        DmaSocket.Insert(new F018());
        FdcSocket.Insert(new F011());
        UartSocket.Insert(new C65Uart());

        Reset();

        if (startClock) Start();
    }

    // Starts the real-time emulation on a separate thread
    public void Start()
    {
        if (clock is not null) return;

        var stop = clockStop = new CancellationTokenSource();
        clock = Task.Factory.StartNew(() =>
        {
            using var timerResolution = new TimerResolution(1);
            var sw = Stopwatch.StartNew();
            long startCycles = totalCycles;

            while (!stop.IsCancellationRequested)
            {
                RunPendingCommands();

                if (resyncClock)
                {
                    resyncClock = false;
                    sw.Restart();
                    startCycles = totalCycles;
                }

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

    // Stops the real-time emulation; the machine keeps its state and Start resumes it
    public void Stop()
    {
        if (clock is null) return;
        clockStop!.Cancel();
        clock.Wait();
        clock = null;
        clockStop.Dispose();
        clockStop = null;
    }

    // Runs a fixed number of cycles synchronously, without the real-time clock (startClock: false): used by the tests
    public void RunCycles(long cycles)
    {
        for (long i = 0; i < cycles; i++) ExecuteCycle();
    }

    void ExecuteCycle()
    {
        if (typingActive) AdvanceTyping();
        cia1?.Tick();
        cia2?.Tick();
        sidRight?.Tick();
        sidLeft?.Tick();
        AudioOut.Sample(((sidRight?.AudioOutput ?? 0) + (sidLeft?.AudioOutput ?? 0)) / 2);
        dma?.Tick();
        fdc?.Tick();
        // The DMAgic and the VIC-III halt the CPU through /DMA and AEC. With FAST it runs at 3.5 MHz: 7 of its cycles
        // every 2 of the board's, alternating 3 and 4
        int cpuCycles = 1;
        if (vic?.Fast == true)
        {
            fastCredit += 7;
            cpuCycles = fastCredit >> 1;
            fastCredit &= 1;
        }
        for (int i = 0; i < cpuCycles; i++) cpu?.Tick();
        vic?.Tick();

        totalCycles++;
        AfterCycle?.Invoke();
    }

    void OnFrameCompleted()
    {
        if (vic is not null) VideoOut.Present(vic);
    }

    // Power on / reset button: /RES reaches all the chips, the CPU restarts from the reset vector
    public void Reset()
    {
        cpuPort.Reset();
        keyPort.Reset();
        uart?.Reset();
        expansion?.Reset();
        cia1?.Reset();
        cia2?.Reset();
        sidRight?.Reset();
        sidLeft?.Reset();
        vic?.Reset();
        dma?.Reset();
        fdc?.Reset();
        cpu?.Reset();
    }

    // The disk in the internal drive (null = no disk). The F011 reads and writes sectors of it
    public IDiskMedia? Disk
    {
        get => fdc?.Media;
        set { if (fdc is not null) fdc.Media = value; }
    }

    // The 128 KB of RAM, bypassing the memory map (for tools and tests)
    public byte Ram(int address) => ram[address];
    public void Ram(int address, byte value) => ram[address] = value;
}
