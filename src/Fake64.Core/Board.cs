using Fake64.Core;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Channels;

namespace Fake64;

public partial class Board
{
    Task clock;

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

    byte[] Rom(string name) => File.ReadAllBytes(Path.Combine("roms", name));

    public MOS6569 VICII => vicii;

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

    public Board(string kernalName = null, string chargenName = null, string basicName = null)
    {
        keyboard = new Keyboard(this);
        kernal = Rom(kernalName ?? nameof(kernal));
        chargen = Rom(chargenName ?? nameof(chargen));
        basic = Rom(basicName ?? nameof(basic));
        ram64 = new(this);
        colorRam = new ColorRamChip(this);
        cia2 = new MOS6526(this, 2);
        cia1 = new MOS6526(this, 1);
        sid = new MOS6581(this);
        vicii = new MOS6569(this);
        cpu = new MOS6510(this);
        MemoryMap();

        Reset();

        // Calculate timing constants for the Stopwatch
        // Stopwatch.Frequency gives ticks per second
        // We need to convert our master clock to Stopwatch ticks
        var ticksPerCycle = Stopwatch.Frequency / MASTER_CLOCK_HZ;

        clock = Task.Factory.StartNew(async () =>
        {
            var sw = Stopwatch.StartNew();
            long lastTicks = 0;

            try
            {

                while (true)
                {
                    long currentTicks = sw.ElapsedTicks;
                    long targetCycles = currentTicks / ticksPerCycle;

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
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Clock task error: {ex.Message}");
            }
        });
    }

    // Perform a single machine cycle
    private void ExecuteCycle()
    {
        // Phase 1: Process all signals and state changes for this cycle
        ProcessSignals();

        // Phase 2: Clock all components for this cycle
        ClockComponents();

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
                ProcessFrame();
            }

            lineStartCycle = totalCycles;

            // Process line completion events
            ProcessLine();
        }
    }

    // Process all signals and pending states
    private void ProcessSignals()
    {
        // Process interrupts and other signals between components
        // This is where components indicate their need to interact with others
        keyboard.ProcessSignals(currentCycle);
        cia1.ProcessSignals(currentCycle);
        cia2.ProcessSignals(currentCycle);
        vicii.ProcessSignals(currentCycle);
        sid.ProcessSignals(currentCycle);
        cpu.ProcessSignals(currentCycle);
    }

    // Clock all components for this cycle
    private void ClockComponents()
    {
        // Each component gets exactly one cycle's worth of processing
        // The order should match the C64 architecture's priority
        vicii.Clock(totalCycles);  // VIC-II has priority for memory access
        sid.Clock(totalCycles);    // SID processes audio
        cia1.Clock(totalCycles);   // CIA chips handle I/O and timers
        cia2.Clock(totalCycles);
        cpu.Clock(totalCycles);    // CPU executes instructions
        keyboard.Clock();          // Poll keyboard state
    }

    // Process end-of-line events
    private void ProcessLine()
    {
        // Handle VIC-II raster interrupts and bad lines
        vicii.EndLine(currentLine);
    }

    // Process end-of-frame events
    private void ProcessFrame()
    {
        // Process frame-level events like screen rendering
        ProcessAudio();
    }

    int audioFrameCounter = 0;
    const int audioFrameRate = 50; // PAL refresh rate (50Hz)

    private void ProcessAudio()
    {
        // Get audio samples from SID and send to audio system
        int[] samples = sid.GetAudioData();
        // Send to audio output system
        // ...
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

    internal void CpuTriggerIRQ()
    {
        cpu.TriggerIRQ();
    }

    internal void CIATriggerInterrupt(int ciaId)
    {
        if (ciaId == 1)
        {
            cpu.TriggerIRQ();
        }
        else if (ciaId == 2)
        {
            cpu.TriggerNMI();
        }
    }

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
        if (addr == 0xE473)
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
        if (addr >= kernal_base)
        {
        }
        else if (addr >= chargen_base)
        {
            if (chargenVisible)
            {
            }
            else if (ioAreaVisible)
            {
                if (addr < 0xd400)
                {
                    vicii.Address((ushort)(addr & 0x03ff), value);
                }
                else if (addr >= 0xdf00) // I/O 2
                {
                }
                else if (addr >= 0xde00) // I/O 1
                {
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
            }
        }
        else if (addr >= 0xc000)
        {
        }
        else if (addr >= basic_base)
        {

        }
        else if (addr >= 0x0400 && addr<0x0500)
        {
        }
        else if (addr <= 0x0001)
        {
            cpu.Address(addr, value);
            MemoryMap();
            return;
        }
        else
        {
        }

        ram64.Address(addr, value);
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

    public byte GetCIAPort(byte id, char port)
    {
        if (id == 1)
        {
            if (port == 'A')
            {
                return port1A;
            }
            else if (port == 'B')
            {
                //return port1B;
                return keyboard.GetKeyboardState(port1A);
            }
        }
        else if (id == 2)
        {
            if (port == 'A')
            {
                return port2A;
            }
            else if (port == 'B')
            {
                return port2B;
            }
        }
        return 0xff;
    }

    public void SetCIAPort(byte id, char port, byte value)
    {
        if (id == 1)
        {
            if (port == 'A')
            {
                port1A = value;
            }
            else if (port == 'B')
            {
                port1B = value;
            }
        }
        else if (id == 2)
        {
            if (port == 'A')
            {
                port2A = value;
            }
            else if (port == 'B')
            {
                port2B = value;
            }
        }
    }

    internal unsafe byte* BeginScreen()
    {
        throw new NotImplementedException();
    }

    internal void EndScreen()
    {
        throw new NotImplementedException();
    }
}
