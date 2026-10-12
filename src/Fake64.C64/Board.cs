using System.Diagnostics;
using Fake64.Mos;

namespace Fake64.C64;

// La scheda madre del C64 (PAL): zoccoli per i chip MOS, RAM, color RAM, ROM, PLA e le piste che
// collegano tutto. I chip non conoscono la scheda: la scheda li collega attraverso la loro piedinatura
public partial class Board
{
    Task? clock;

    readonly Keyboard keyboard = new();
    readonly Ram64KbChip ram64 = new();
    readonly ColorRamChip colorRam = new();

    // Prese esterne: uscita video (vi si collega un monitor con un MonitorCable), tastiera
    // e le due porte di controllo (joystick, paddle, mouse)
    public VideoPort VideoOut { get; } = new();
    public Keyboard Keyboard => keyboard;
    public ControlPort ControlPort1 { get; } = new("porta di controllo 1");
    public ControlPort ControlPort2 { get; } = new("porta di controllo 2");

    // Accesso diretto alla RAM, senza passare per la mappa di memoria (come un DMA): permette di
    // scrivere anche sotto le ROM. Da usare dal thread di emulazione, ad esempio tramite Enqueue
    public Ram64KbChip Ram => ram64;

    internal long TotalCycles => totalCycles;

    // Cicli con BA basso: il VIC-II ha chiesto il bus (43 per bad line; la CPU può ancora scrivere nei primi 3)
    internal long StolenCycles { get; private set; }

    // Cycle constants for PAL C64
    private const long MASTER_CLOCK_HZ = 985248; // PAL master clock frequency in Hz

    private long totalCycles;

    // Le ROM arrivano da fuori: la Board non sa dove sono i file (vedi SystemRoms).
    // Gli zoccoli vengono popolati con i chip di serie; si possono sostituire (vedi Socket)
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

    // Un ciclo di clock (φ2). BA del VIC-II si legge prima di farlo avanzare, perché riguarda
    // il ciclo in corso; il VIC-II chiude il ciclo e, a fine riga, disegna e gestisce il raster
    private void ExecuteCycle()
    {
        bool busAvailable = vic?.BusAvailable ?? true;

        cia1?.Tick();
        cia2?.Tick();
        sid?.Tick();
        cpu?.Tick(); // RDY e AEC sono collegati al VIC-II: la CPU si ferma da sola quando serve
        if (!busAvailable) StolenCycles++;
        vic?.Tick();

        totalCycles++;
    }

    // Fine di un frame del VIC-II: l'immagine va alla presa video, l'audio del SID (non ancora riprodotto)
    // viene scaricato per non riempire il buffer
    void OnFrameCompleted()
    {
        if (vic is not null) VideoOut.Present(vic);
        sid?.GetAudioData();
    }

    // Accensione / pulsante di reset: /RES arriva a tutti i chip, la CPU riparte dal vettore di reset
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
        cpu?.Reset(); // la porta $01 torna in ingresso: la mappa si aggiorna prima di leggere il vettore
    }

    public byte Chargen(ushort addr) => chargen[addr];

    const ushort kernal_base = 0xE000;
    readonly byte[] kernal;
    const ushort chargen_base = 0xD000;
    readonly byte[] chargen;
    const ushort basic_base = 0xA000;
    readonly byte[] basic;
}
