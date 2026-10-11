using System.Diagnostics;

namespace Fake64;

public partial class Board
{
    Task clock;

    IScreen screen;
    Keyboard keyboard;

    MOS6510 cpu;
    MOS6569 vicii;
    MOS6581 sid;
    MOS6526 cia1;
    MOS6526 cia2;
    ColorRamChip colorRam;

    const byte addr_shift = 13;
    const ushort addr_mask = 0x1fff;

    Ram64KbChip ram64;

    byte[] Rom(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "roms", name));

    public MOS6569 VICII => vicii;

    // Accesso ai chip per i test (InternalsVisibleTo Fake64.Tests)
    internal MOS6510 Cpu => cpu;
    internal MOS6526 Cia1 => cia1;
    internal MOS6526 Cia2 => cia2;
    internal long TotalCycles => totalCycles;

    // Cicli in cui la CPU è stata fermata dal VIC-II (bad line)
    internal long StolenCycles { get; private set; }

    // Cycle constants for PAL C64
    private const long MASTER_CLOCK_HZ = 985248; // PAL master clock frequency in Hz
    private const long CYCLES_PER_FRAME = 19656; // PAL: 312 lines × 63 cycles
    private const long CYCLES_PER_LINE = 63;     // Cycles per raster line
    private const long CYCLES_PER_SECOND = 50;   // PAL refresh rate in Hz

    // Timing trackers
    private long totalCycles;
    private long frameStartCycle;
    private long lineStartCycle;
    private int currentLine;
    private int currentCycle;

    public Board(IScreen screen, string kernalName = null, string chargenName = null, string basicName = null, bool startClock = true)
    {
        this.screen = screen;
        keyboard = new Keyboard(this);
        kernal = Rom(kernalName ?? nameof(kernal));
        chargen = Rom(chargenName ?? nameof(chargen));
        basic = Rom(basicName ?? nameof(basic));
        ram64 = new(this);
        colorRam = new ColorRamChip(this);
        // In Board.cs constructor or initialization
        cia1 = new MOS6526(
            this,
            // CIA1 pilota /IRQ: la CPU legge la linea tramite IrqLine
            null,
            // Port read handler for CIA1
            (port) => port == 'A' ? port1A : keyboard.GetKeyboardState(port1A),
            // Port write handler for CIA1
            (port, value) => {
                if (port == 'A') port1A = value;
                else port1B = value;
            },
            // CIA1-specific initialization
            (cia) => {
                // Set Timer A for ~60Hz jiffy interrupt
                cia.TimerALatch = 16421; // Use 16421 for PAL, 17045 for NTSC
                cia.ControlRegA = 0x01; // Bit 0: Start timer
                cia.InterruptControl = 0x81; // Bit 0: Enable Timer A interrupts, Bit 7: Set mask bit
            }
        );

        cia2 = new MOS6526(
            this,
            // CIA2 è collegata a /NMI sul C64
            () => cpu.TriggerNMI(),
            // Port read handler for CIA2
            (port) => port == 'A' ? port2A : port2B,
            // Port write handler for CIA2
            (port, value) => {
                if (port == 'A') port2A = value;
                else port2B = value;
            }
        // No special initialization needed for CIA2
        );
        sid = new MOS6581(this);
        vicii = new MOS6569(this, () => {
            Render();
        });
        cpu = new MOS6510(this);
        MemoryMap();

        Reset();

        if (startClock) Start();
    }

    // Avvia l'emulazione in tempo reale su un task separato
    public void Start()
    {
        if (clock is not null) return;

        clock = Task.Factory.StartNew(async () =>
        {
            var sw = Stopwatch.StartNew();
            long startCycles = totalCycles;

            while (true)
            {
                // Converte il tempo trascorso in cicli PAL senza perdere precisione
                long targetCycles = startCycles + (long)(sw.ElapsedTicks * (double)MASTER_CLOCK_HZ / Stopwatch.Frequency);

                // Run as many cycles as needed to catch up to wall clock
                while (totalCycles < targetCycles)
                {
                    // Run one machine cycle
                    ExecuteCycle();

                    // Throttle if we're getting too far ahead
                    if (totalCycles % 1000 == 0 && totalCycles > targetCycles + 10000)
                    {
                        await Task.Delay(1);
                    }
                }
            }
        }).Unwrap();
    }

    // Esegue un numero fisso di cicli in modo sincrono (usato dai test, con startClock: false)
    internal void RunCycles(long cycles)
    {
        for (long i = 0; i < cycles; i++) ExecuteCycle();
    }

    // Esegue cicli fino al completamento dell'istruzione corrente e all'inizio della successiva
    internal void StepInstruction()
    {
        long start = cpu.InstructionCount;
        do ExecuteCycle(); while (cpu.InstructionCount == start || !cpu.IsInstructionBoundary);
    }

    // Perform a single machine cycle
    unsafe private void ExecuteCycle()
    {
        // Phase 1: Process all signals and state changes for this cycle
        // Process interrupts and other signals between components
        // This is where components indicate their need to interact with others
        keyboard.ProcessSignals(currentCycle);
        cia1.ProcessSignals(currentCycle);
        cia2.ProcessSignals(currentCycle);
        vicii.ProcessSignals(currentCycle);
        sid.ProcessSignals(currentCycle);

        // Nelle bad line il VIC-II tiene BA basso e la CPU resta ferma per 40 cicli
        bool cpuStalled = vicii.CpuStalled;
        if (!cpuStalled) cpu.ProcessSignals(currentCycle);

        // Phase 2: Clock all components for this cycle
        // Each component gets exactly one cycle's worth of processing
        // The order should match the C64 architecture's priority
        vicii.Clock(totalCycles);  // VIC-II has priority for memory access
        sid.Clock(totalCycles);    // SID processes audio
        cia1.Clock(totalCycles);   // CIA chips handle I/O and timers
        cia2.Clock(totalCycles);
        if (cpuStalled) StolenCycles++;
        else cpu.Clock(totalCycles);    // CPU executes instructions
        keyboard.Clock();          // Poll keyboard state

        // Update cycle counters
        totalCycles++;
        currentCycle++;

        // Check for line boundary
        if (currentCycle >= CYCLES_PER_LINE)
        {
            currentCycle = 0;
            currentLine++;

            // Check for frame boundary
            if (currentLine >= 312) // PAL has 312 lines
            {
                currentLine = 0;
                frameStartCycle = totalCycles;

                // Process frame completion events
                // Process frame-level events like screen rendering
                Render();

                // Get audio samples from SID and send to audio system
                int[] samples = sid.GetAudioData();
                // Send to audio output system
                // ...
            }

            lineStartCycle = totalCycles;

            // Process line completion events
            // Handle VIC-II raster interrupts and bad lines
            vicii.EndLine(currentLine);
        }
    }

    unsafe void Render()
    {
        if (screen is null) return;
        var scan0 = screen.BeginRender();
        vicii.Render(scan0);
        screen.EndRender();
    }

    public void Reset()
    {
        ram64.Reset();
        colorRam.Reset();
        cia2.Reset();
        cia1.Reset();
        sid.Reset();
        vicii.Reset();
        cpu.Reset();

        totalCycles = 0;
        currentLine = 0;
        currentCycle = 0;
        frameStartCycle = 0;
        lineStartCycle = 0;
    }

    // Linea /IRQ condivisa (wired-OR): attiva finché almeno una sorgente la tiene bassa
    internal bool IrqLine => cia1.IrqAsserted || vicii.IrqAsserted;

    public byte Chargen(ushort addr) => chargen[addr];

    /*
        Bits #0-#2: Configuration for memory areas $A000-$BFFF, $D000-$DFFF and $E000-$FFFF. Values:
        %x00: RAM visible in all three areas.
        %x01: RAM visible at $A000-$BFFF and $E000-$FFFF.
        %x10: RAM visible at $A000-$BFFF; KERNAL ROM visible at $E000-$FFFF.
        %x11: BASIC ROM visible at $A000-$BFFF; KERNAL ROM visible at $E000-$FFFF.
        %0xx: Character ROM visible at $D000-$DFFF. (Except for the value %000, see above.)
        %1xx: I/O area visible at $D000-$DFFF. (Except for the value %100, see above.)
     */

    void MemoryMap()
    {
        var state = cpu.Address(0x01);
        ramVisible = ((state & (byte)0b00000011) == 0b00000000);
        kernalVisible = ((state & (byte)0b00000010) == 0b00000010);
        basicRomVisible = ((state & (byte)0b00000011) == 0b00000011);

        chargenVisible = ((state & (byte)0b00000100) == 0b00000000) && !ramVisible;
        ioAreaVisible = ((state & (byte)0b00000100) == 0b00000100) && !ramVisible;
    }

    bool ramVisible;
    bool kernalVisible;
    bool basicRomVisible;

    bool chargenVisible;
    bool ioAreaVisible;

    public byte Address(ushort addr)
    {
        if (addr == 0xea1e)
        {
        }
        
        if (addr >= kernal_base)
        {
            if (kernalVisible)
            {
                return kernal[addr - kernal_base];
            }
        }
        else if (addr >= chargen_base)
        {
            if (chargenVisible)
            {
                return chargen[addr - chargen_base];
            }
            else if (ioAreaVisible)
            {
                if (addr < 0xd400)
                {
                    return vicii.Address((ushort)(addr & 0x03ff));
                }
                else if (addr >= 0xdf00) // I/O 2
                {
                }
                else if (addr >= 0xde00) // I/O 1
                {
                }
                else if (addr >= 0xdd00) // CIA 2
                {
                    return cia2.Address((ushort)(addr & 0x00ff));
                }
                else if (addr >= 0xdc00) // CIA 1
                {
                    return cia1.Address((ushort)(addr & 0x00ff));
                }
                else if (addr >= 0xd800) // Color RAM
                {
                    return colorRam.Address((ushort)(addr & 0x03ff));
                }
                else if (addr >= 0xd400) // SID
                {
                    return sid.Address((ushort)(addr & 0x03ff));
                }
                else
                {
                    return vicii.Address((ushort)(addr & 0x03ff));
                }
            }
            else
            {
            }
        }
        else if (addr >= 0xc000)
        {
        }
        else if (addr >= basic_base)
        {
            if (basicRomVisible)
            {
                return basic[addr - basic_base];
            }
            else
            {
            }
        }
        else if (addr <= 0x0001)
        {
            return cpu.Address(addr);
        }
        else
        {
        }
        //return ram[addr >> addr_shift].Address((ushort)(addr & addr_mask));
        return ram64.Address(addr);
    }

    public void Address(ushort addr, byte value)
    {
        if (addr == 0xea1e)
        {
        }

        if (addr >= kernal_base)
        {
            // Writing to KERNAL ROM space falls through to RAM
            ram64.Address(addr, value);
        }
        else if (addr >= chargen_base)
        {
            if (chargenVisible)
            {
                // Character ROM is read-only, writes fall through to RAM
                ram64.Address(addr, value);
            }
            else if (ioAreaVisible)
            {
                if (addr < 0xd400)
                {
                    vicii.Address((ushort)(addr & 0x03ff), value);
                }
                else if (addr >= 0xdf00) // I/O 2
                {
                    // I/O expansion area 2, typically empty
                    ram64.Address(addr, value);
                }
                else if (addr >= 0xde00) // I/O 1
                {
                    // I/O expansion area 1, typically empty
                    ram64.Address(addr, value);
                }
                else if (addr >= 0xdd00) // CIA 2
                {
                    cia2.Address((ushort)(addr & 0x00ff), value);
                    return;
                }
                else if (addr >= 0xdc00) // CIA 1
                {
                    cia1.Address((ushort)(addr & 0x00ff), value);
                    return;
                }
                else if (addr >= 0xd800) // Color RAM
                {
                    colorRam.Address((ushort)(addr & 0x03ff), value);
                    return;
                }
                else if (addr >= 0xd400) // SID
                {
                    sid.Address((ushort)(addr & 0x03ff), value);
                    return;
                }
                else
                {
                    vicii.Address((ushort)(addr & 0x03ff), value);
                    return;
                }
            }
            else
            {
                // RAM underneath when I/O and CHARGEN aren't visible
                ram64.Address(addr, value);
            }
        }
        else if (addr >= 0xc000)
        {
            // RAM area between BASIC ROM and CHARGEN
            ram64.Address(addr, value);
        }
        else if (addr >= basic_base)
        {
            // BASIC ROM area - writes go to RAM underneath
            ram64.Address(addr, value);
        }
        else if (addr >= 0x0400 && addr<0x0500)
        {
            // Screen memory - special handling if needed
            ram64.Address(addr, value);
        }
        else if (addr <= 0x0001)
        {
            cpu.Address(addr, value);
            MemoryMap();
            return;
        }
        else
        {
            // All other areas are normal RAM
            ram64.Address(addr, value);
        }
    }

    const ushort kernal_base = 0xE000;
    byte[] kernal;
    const ushort chargen_base = 0xD000;
    byte[] chargen;
    const ushort basic_base = 0xA000;
    byte[] basic;

    byte port1A;
    byte port1B;
    byte port2A;
    byte port2B;
}
