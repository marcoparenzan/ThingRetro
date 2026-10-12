namespace Fake64.Mos;

// Piedinature dei chip: ognuna è il contratto di uno zoccolo. Qualunque oggetto che la implementa
// "entra" nello zoccolo, come un chip compatibile elettricamente (un 8580 al posto di un 6581,
// un sostituto moderno, una sonda che registra il traffico...).
// Modello a transazioni: i pin di indirizzo, dati, R/W e chip select sono raggruppati in Read/Write
// (il chip viene interpellato solo quando è selezionato); clock e reset sono Tick e Reset;
// le linee di controllo sono proprietà. Le linee attive basse (/IRQ, /NMI, /RES) sono "true = attiva".

// Bus indirizzi/dati visto da chi lo pilota (A0-A15, D0-D7, R/W)
public interface IBus
{
    byte Read(ushort address);
    void Write(ushort address, byte value);
}

// MOS 6510, DIP-40: la CPU del C64 (6502 con porta di I/O integrata)
public interface I6510Pinout
{
    IBus? Bus { get; set; }              // A0-A15, D0-D7, R/W
    Func<bool> IrqInput { get; set; }    // /IRQ (a livello)
    Func<bool> NmiInput { get; set; }    // /NMI (sul fronte di discesa)
    Func<bool> RdyInput { get; set; }    // RDY: falso = la CPU si ferma al primo ciclo di lettura (BA del VIC-II)
    Func<bool> AecInput { get; set; }    // AEC: falso = il bus è di un altro (il VIC-II), la CPU è ferma
    ParallelPort Port { get; }           // P0-P5, registri $0000 (direzione) e $0001 (dati)
    void Reset();                        // /RES
    void Tick();                         // un ciclo di φ2
}

// MOS 6526 CIA, DIP-40: timer, orologio, porte parallele, linea seriale
public interface I6526Pinout
{
    byte Read(int register);             // RS0-RS3, D0-D7, R/W = 1, /CS
    void Write(int register, byte value);
    ParallelPort PortA { get; }          // PA0-PA7
    ParallelPort PortB { get; }          // PB0-PB7
    bool Irq { get; }                    // /IRQ
    void Reset();                        // /RES
    void Tick();                         // φ2
    // Non modellati: CNT, SP, FLAG, /PC, TOD (il TOD è contato internamente)
}

// Memoria vista dal VIC-II: 14 bit di indirizzo (A0-A13) e il nibble di colore (D8-D11).
// I bit A14/A15 (il banco) li aggiunge la scheda, che sa da dove arrivano
public interface IVicMemory
{
    byte Read(int address);              // A0-A13 -> D0-D7
    byte ReadColor(int address);         // A0-A9 -> D8-D11 (color RAM)
}

// MOS 6569 VIC-II (PAL), DIP-40: grafica, raster, DMA sul bus
public interface I6569Pinout
{
    byte Read(int register);             // A0-A5, D0-D7, R/W = 1, /CS
    void Write(int register, byte value);
    IVicMemory? Memory { get; set; }     // bus video
    bool Irq { get; }                    // /IRQ
    bool BusAvailable { get; }           // BA: falso = il VIC-II chiederà il bus fra 3 cicli (la CPU finisce le scritture)
    bool AddressEnable { get; }          // AEC: falso = il VIC-II sta usando il bus anche nella fase della CPU
    Action? FrameCompleted { get; set; } // uscita video: un frame completo è pronto
    unsafe void Render(byte* pixels);    // copia l'ultimo frame (SCREEN_WIDTH x SCREEN_HEIGHT, BGRA)
    void Reset();                        // nessun /RES sul VIC-II: azzera lo stato all'accensione
    void Tick();                         // un ciclo di φ0
}

// MOS 6581 SID, DIP-28: tre voci, inviluppi, filtro
public interface I6581Pinout
{
    byte Read(int register);             // A0-A4, D0-D7, R/W = 1, /CS
    void Write(int register, byte value);
    int[] GetAudioData();                // AUDIO OUT
    void Reset();                        // /RES
    void Tick();                         // φ2
}
