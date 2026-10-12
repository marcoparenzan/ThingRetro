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
    internal Keyboard Keyboard => keyboard;
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

        // Thread dedicato: emula i cicli che mancano per stare al passo con l'orologio, poi dorme 1 ms
        // invece di girare a vuoto (prima il ciclo teneva occupato un core anche a emulazione in pari)
        clock = Task.Factory.StartNew(() =>
        {
            using var timerResolution = new TimerResolution(1);
            var sw = Stopwatch.StartNew();
            long startCycles = totalCycles;

            while (true)
            {
                // Comandi arrivati da altri thread (es. caricamento di un .prg dalla UI)
                RunPendingCommands();

                // Converte il tempo trascorso in cicli PAL senza perdere precisione
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

                // Il frame viene presentato dal VIC-II alla fine dell'ultima riga (callback render)

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
        cartridge?.Reset();
        cpu.Reset();
        MemoryMap(); // la CPU riparte con $01 = $37 e le linee iniziali della cartuccia
        cpu.ReadResetVector();

        // totalCycles non viene azzerato: il clock in tempo reale lo usa come riferimento
        currentLine = 0;
        currentCycle = 0;
        frameStartCycle = 0;
        lineStartCycle = 0;
    }

    // Linea /IRQ condivisa (wired-OR): attiva finché almeno una sorgente la tiene bassa
    internal bool IrqLine => cia1.IrqAsserted || vicii.IrqAsserted;

    public byte Chargen(ushort addr) => chargen[addr];

    // Banco di 16 KB visto dal VIC-II: bit 0-1 della porta A della CIA2, invertiti
    internal int VicBank => 3 - ((port2A | ~cia2.DataDirectionA) & 0x03);

    // Lettura dal punto di vista del VIC-II: vede sempre la RAM (mai BASIC, KERNAL o I/O),
    // tranne la ROM caratteri a $1000-$1FFF nei banchi 0 e 2
    internal byte VicRead(int bank, int addr)
    {
        addr &= 0x3FFF;
        if (ultimax)
        {
            // In Ultimax il VIC-II vede ROMH ($F000-$FFFF della cartuccia) a $3000-$3FFF di ogni banco
            if ((addr & 0x3000) == 0x3000) return cartridge!.ReadRomh(0x1000 | addr & 0x0FFF);
        }
        else if ((bank & 1) == 0 && (addr & 0x3000) == 0x1000)
            return chargen[addr & 0x0FFF];
        return ram64.Address((ushort)(bank << 14 | addr));
    }

    internal byte VicColorRam(int index) => (byte)(colorRam.Address((ushort)index) & 0x0F);

    /*
        Il PLA decide cosa vede la CPU combinando i bit 0-2 della porta $01 (LORAM, HIRAM, CHAREN)
        con le linee GAME ed EXROM della porta di espansione (attive basse, alte senza cartuccia):

        Senza cartuccia (EXROM = 1, GAME = 1):
          $A000 BASIC se LORAM e HIRAM, $E000 KERNAL se HIRAM,
          $D000 I/O se CHAREN altrimenti ROM caratteri (tutto RAM se LORAM = HIRAM = 0)
        Cartuccia 8K (EXROM = 0, GAME = 1):  in più ROML a $8000 se LORAM e HIRAM
        Cartuccia 16K (EXROM = 0, GAME = 0): in più ROMH a $A000 (al posto del BASIC) se HIRAM
        Ultimax (EXROM = 1, GAME = 0): ROML a $8000, ROMH a $E000, I/O sempre a $D000,
          $1000-$7FFF e $A000-$CFFF scollegati; la porta $01 viene ignorata
     */

    void MemoryMap()
    {
        var state = cpu.Address(0x01);
        bool loram = (state & 0x01) != 0;
        bool hiram = (state & 0x02) != 0;
        bool charen = (state & 0x04) != 0;
        bool exrom = cartridge?.Exrom ?? true;
        bool game = cartridge?.Game ?? true;

        ultimax = exrom && !game;
        if (ultimax)
        {
            romlVisible = true;
            romhAtA000 = false;
            basicRomVisible = false;
            kernalVisible = false;
            chargenVisible = false;
            ioAreaVisible = true;
            return;
        }

        bool allRam = !loram && !hiram;
        romlVisible = !exrom && loram && hiram;
        romhAtA000 = !exrom && !game && hiram;
        basicRomVisible = game && loram && hiram;
        kernalVisible = hiram;
        chargenVisible = !charen && !allRam;
        ioAreaVisible = charen && !allRam;
    }

    bool kernalVisible;
    bool basicRomVisible;
    bool chargenVisible;
    bool ioAreaVisible;
    bool romlVisible;
    bool romhAtA000;
    bool ultimax;

    // In modalità Ultimax queste aree non sono collegate a niente
    bool IsUnmappedInUltimax(ushort addr) =>
        ultimax && ((addr >= 0x1000 && addr < 0x8000) || (addr >= 0xA000 && addr < 0xD000));

    public byte Address(ushort addr)
    {
        if (addr <= 0x0001)
            return cpu.Address(addr);
        if (IsUnmappedInUltimax(addr))
            return 0xFF;

        switch (addr >> 12)
        {
            case 0x8:
            case 0x9:
                if (romlVisible) return cartridge!.ReadRoml(addr & 0x1FFF);
                break;
            case 0xA:
            case 0xB:
                if (romhAtA000) return cartridge!.ReadRomh(addr & 0x1FFF);
                if (basicRomVisible) return basic[addr - basic_base];
                break;
            case 0xD:
                if (ioAreaVisible) return ReadIo(addr);
                if (chargenVisible) return chargen[addr - chargen_base];
                break;
            case 0xE:
            case 0xF:
                if (ultimax) return cartridge!.ReadRomh(addr & 0x1FFF);
                if (kernalVisible) return kernal[addr - kernal_base];
                break;
        }
        return ram64.Address(addr);
    }

    public void Address(ushort addr, byte value)
    {
        if (addr <= 0x0001)
        {
            cpu.Address(addr, value);
            MemoryMap();
            return;
        }
        if (IsUnmappedInUltimax(addr))
            return;

        if ((addr >> 12) == 0xD && ioAreaVisible)
        {
            WriteIo(addr, value);
            return;
        }

        // Le scritture nelle aree ROM (BASIC, KERNAL, caratteri, cartuccia) finiscono nella RAM sottostante
        ram64.Address(addr, value);
    }

    byte ReadIo(ushort addr)
    {
        if (addr < 0xD400) return vicii.Address((ushort)(addr & 0x03ff));
        if (addr < 0xD800) return sid.Address((ushort)(addr & 0x03ff));
        if (addr < 0xDC00) return colorRam.Address((ushort)(addr & 0x03ff));
        if (addr < 0xDD00) return cia1.Address((ushort)(addr & 0x00ff));
        if (addr < 0xDE00) return cia2.Address((ushort)(addr & 0x00ff));
        if (addr < 0xDF00) return cartridge?.ReadIo1(addr & 0xFF) ?? ram64.Address(addr); // I/O 1
        return cartridge?.ReadIo2(addr & 0xFF) ?? ram64.Address(addr);                    // I/O 2
    }

    void WriteIo(ushort addr, byte value)
    {
        if (addr < 0xD400) vicii.Address((ushort)(addr & 0x03ff), value);
        else if (addr < 0xD800) sid.Address((ushort)(addr & 0x03ff), value);
        else if (addr < 0xDC00) colorRam.Address((ushort)(addr & 0x03ff), value);
        else if (addr < 0xDD00) cia1.Address((ushort)(addr & 0x00ff), value);
        else if (addr < 0xDE00) cia2.Address((ushort)(addr & 0x00ff), value);
        else if (addr < 0xDF00) { if (cartridge is null) ram64.Address(addr, value); else cartridge.WriteIo1(addr & 0xFF, value); }
        else { if (cartridge is null) ram64.Address(addr, value); else cartridge.WriteIo2(addr & 0xFF, value); }
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
