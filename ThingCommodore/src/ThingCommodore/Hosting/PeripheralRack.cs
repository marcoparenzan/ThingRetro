using ThingCommodore.Drives;
using ThingCommodore.Printers;
using ThingCommodore.Tape;
using ThingRetroEmu.Ports;

namespace ThingCommodore.Hosting;

/// <summary>
/// The peripherals of a Commodore computer, on their cables: the disk drives (1541, 1581) and the printer on the IEC cable, the cassette recorder on the cassette
/// connector. The rack makes the cable, puts each device on it and clocks them with the computer. Every change is made on the emulation thread (through
/// <c>enqueue</c>), between two cycles; what the properties tell can be read from any thread
/// </summary>
public sealed class PeripheralRack
{
    public const long ComputerHz = 985_248;

    readonly IecPort iec;
    readonly Action<Action> enqueue;
    readonly Action<Action?> setAfterCycle;
    readonly Func<string, byte[]?> roms;
    readonly byte[] chargen;

    Mps803? printer;
    int printerDevice;

    /// <param name="iec">The serial connector of the computer.</param>
    /// <param name="cassette">Its cassette connector, or null if it has none (a C65).</param>
    /// <param name="enqueue">Runs an action on the emulation thread (<c>board.Enqueue</c>).</param>
    /// <param name="setAfterCycle">Sets what the computer calls at the end of every cycle (<c>board.AfterCycle</c>).</param>
    /// <param name="roms">Gives the ROM of a drive by file name (<c>c-drive-1541.rom</c>, <c>c-drive-1581.rom</c>), or null if it is not there.</param>
    /// <param name="chargen">The character generator of the computer, for the printer.</param>
    public PeripheralRack(IecPort iec, CassettePort? cassette, Action<Action> enqueue, Action<Action?> setAfterCycle, Func<string, byte[]?> roms, byte[] chargen)
    {
        this.iec = iec;
        this.enqueue = enqueue;
        this.setAfterCycle = setAfterCycle;
        this.roms = roms;
        this.chargen = chargen;
        Disk1541 = new DriveSlot1541(this);
        Disk1581 = new DriveSlot1581(this);
        if (cassette is not null)
        {
            Tape.Plug(cassette);
            HasCassette = true;
        }
    }

    public DriveSlot1541 Disk1541 { get; }
    public DriveSlot1581 Disk1581 { get; }

    /// <summary>The C2N: always there (a C64 has the connector for it); without a computer connector it does nothing.</summary>
    public Datasette Tape { get; } = new();
    public bool HasCassette { get; }
    public string? TapePath { get; internal set; }

    public bool TapeModified => Tape.Tape?.Modified == true;

    /// <summary>Puts the tape of a .tap file in the recorder. Throws if the file is not a tape.</summary>
    public void InsertTape(string file)
    {
        var image = new ThingRetroEmu.Media.TapImage(File.ReadAllBytes(file));
        enqueue(() =>
        {
            WriteTapeBack();
            Tape.Tape = image;
            TapePath = file;
        });
    }

    /// <summary>Puts a blank tape in the recorder.</summary>
    public void NewTape() => enqueue(() =>
    {
        WriteTapeBack();
        Tape.Tape = new ThingRetroEmu.Media.TapImage();
        TapePath = null;
    });

    public void EjectTape() => enqueue(() =>
    {
        WriteTapeBack();
        Tape.Tape = null;
        TapePath = null;
    });

    public void PressTape(DatasetteKey key) => enqueue(() => Tape.Press(key));

    /// <summary>Writes the tape to the file it came from, if it was recorded on.</summary>
    public void SaveTape() => enqueue(WriteTapeBack);

    public void SaveTapeAs(string file, Action<Exception>? failed = null) => enqueue(() =>
    {
        if (Tape.Tape is null) return;
        try
        {
            File.WriteAllBytes(file, Tape.Tape.ToBytes());
            Tape.Tape.MarkSaved();
            TapePath = file;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { failed?.Invoke(e); }
    });

    void WriteTapeBack()
    {
        if (Tape.Tape is not { Modified: true } image || TapePath is null) return;
        File.WriteAllBytes(TapePath, image.ToBytes());
        image.MarkSaved();
    }

    /// <summary>Writes every changed disk and tape back to its file; for when the program ends. Runs on the calling thread: stop the board first.</summary>
    public void SaveAll()
    {
        Disk1541.SaveNow();
        Disk1581.SaveNow();
        WriteTapeBack();
    }

    public byte[]? Rom(string fileName) => roms(fileName);
    internal void Enqueue(Action action) => enqueue(action);

    // ---- the printer ------------------------------------------------------------------------------------------------------

    /// <summary>The device number of the printer, or 0 if it is not on the cable.</summary>
    public int PrinterDevice => printerDevice;

    public int PrinterSheets => printer is { HasPrinted: true } ? printer.Paper.Sheets : 0;

    /// <summary>Puts the printer on the cable as device 4 or 5, or takes it off (0). The paper stays in it.</summary>
    public void SetPrinter(int device)
    {
        if (device is not (0 or 4 or 5)) throw new ArgumentException($"{device} is not a device number of a printer (4 or 5)");
        printerDevice = device;
        enqueue(() =>
        {
            if (device == 0) printer?.Unplug();
            else
            {
                printer ??= new Mps803(chargen, device);
                printer.DeviceNumber = device;
            }
            Rewire();
        });
    }

    /// <summary>Writes what was printed as a PDF (on the emulation thread, so that the paper does not change while it is written).</summary>
    public void SavePrinterPdf(string path, Action<Exception>? failed = null) => enqueue(() =>
    {
        try { File.WriteAllBytes(path, printer?.ToPdf() ?? []); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { failed?.Invoke(e); }
    });

    public void NewPaper() => enqueue(() => printer?.NewPaper());

    // ---- the cables and the clock -----------------------------------------------------------------------------------------

    /// <summary>
    /// Makes the IEC cable again with each device that is on it, and puts their clocks together. Called, on the emulation thread, whenever a
    /// device comes or goes
    /// </summary>
    internal void Rewire()
    {
        var cable = new ThingRetroEmu.Buses.IecBus();
        cable.Attach(() => iec.AtnLow(), () => iec.ClkLow(), () => iec.DataLow(),
            input => iec.ClkInputLow = input, input => iec.DataInputLow = input);

        Action? cycle = null;
        cycle += Disk1541.Connect(cable);
        cycle += Disk1581.Connect(cable);
        if (printer is not null && printerDevice != 0)
        {
            printer.Unplug();
            printer.Plug(cable);
            cycle += printer.Tick;
        }
        if (HasCassette) cycle += Tape.Tick;
        setAfterCycle(cycle);
    }
}
