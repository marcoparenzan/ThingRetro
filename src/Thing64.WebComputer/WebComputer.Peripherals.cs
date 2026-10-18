using Thing64.C64;
using ThingCommodore.Printers;
using ThingCommodore.Tape;
using ThingRetroEmu.Buses;
using ThingRetroEmu.Media;

namespace Thing64.WebComputer;

// The peripherals of the C64 in the browser besides the 1541: the C2N cassette recorder on the cassette connector and the MPS-803
// printer on the IEC cable (ThingCommodore). They are machines of their own; the page only hands them the computer's connectors and
// the cycles. Everything that is clocked with the computer (the drive, the printer, the recorder) runs from Board.AfterCycle, which is
// put together again by Rewire whenever one of them comes or goes. The IEC cable too is made again then, with the computer's end
// and each device that is on it
public static partial class WebComputer
{
    static SystemRoms? systemRoms;
    static IecBus? iecCable;

    // ---- the cables and the clock -------------------------------------------------------------------------------------------

    static void Rewire()
    {
        if (board is null) return;
        var computer = board;
        iecCable = new IecBus();
        iecCable.Attach(() => computer.Iec.AtnLow(), () => computer.Iec.ClkLow(), () => computer.Iec.DataLow(),
            input => computer.Iec.ClkInputLow = input, input => computer.Iec.DataInputLow = input);

        Action? cycle = null;
        if (drive is not null)
        {
            drive.Unplug();
            drive.Plug(iecCable);
            cycle += driveClock!.OnComputerCycle;
        }
        if (drive81 is not null)
        {
            drive81.Unplug();
            drive81.Plug(iecCable);
            cycle += drive81Clock!.OnComputerCycle;
        }
        if (printer is not null)
        {
            printer.Unplug();
            printer.Plug(iecCable);
            cycle += printer.Tick;
        }
        tape.Plug(computer.Cassette);
        cycle += tape.Tick;
        computer.AfterCycle = cycle;
    }

    // ---- the cassette recorder ----------------------------------------------------------------------------------------------

    static readonly Datasette tape = new();
    static string? tapeName;

    public static bool HasTape => tape.Tape is not null;
    public static string? TapeName => tapeName;
    public static string TapeKey => tape.Key.ToString();

    // True when the program recorded something on the tape since it was put in or saved
    public static bool TapeModified => tape.Tape?.Modified == true;

    // A .tap file goes into the recorder; the keys are the person's (the KERNAL asks to press them)
    public static void InsertTape(string name, byte[] content)
    {
        var image = new TapImage(content);
        tape.Stop();
        tape.Tape = image;
        tapeName = name;
    }

    // A blank cassette
    public static void NewTape(string name = "tape.tap")
    {
        tape.Stop();
        tape.Tape = new TapImage();
        tapeName = name;
    }

    public static void EjectTape()
    {
        tape.Stop();
        tape.Tape = null;
        tapeName = null;
    }

    // "Play", "Record", "Rewind", "FastForward" or "Stop"
    public static void PressTape(string key)
    {
        if (Enum.TryParse(key, ignoreCase: true, out DatasetteKey parsed)) tape.Press(parsed);
    }

    public static byte[] TapeBytes() => tape.Tape?.ToBytes() ?? [];

    public static void TapeSaved() => tape.Tape?.MarkSaved();

    // "no tape", "tape.tap: stopped, 0:12", "tape.tap: PLAY, 1:05 (changed)"
    public static string TapeText()
    {
        if (tape.Tape is null) return "no tape";
        int seconds = (int)tape.Seconds;
        string key = tape.Key == DatasetteKey.Stop ? "stopped" : tape.Key.ToString().ToUpperInvariant();
        return $"{tapeName}: {key}, {seconds / 60}:{seconds % 60:D2}{(TapeModified ? " (changed)" : "")}";
    }

    // ---- the printer ------------------------------------------------------------------------------------------------------

    static Mps803? printer;

    public static bool PrinterOn => printer is not null;
    public static int PrinterDevice => printer?.DeviceNumber ?? 0;
    public static int PrinterSheets => printer is { HasPrinted: true } ? printer.Paper.Sheets : 0;

    // Plugs the printer in as device 4 or 5 (0 takes it off the cable); the paper stays in it
    public static void SetPrinter(int device)
    {
        if (device == 0)
        {
            printer?.Unplug();
            printer = null;
            Rewire();
            return;
        }
        if (device is not (4 or 5)) throw new ArgumentException($"{device} is not a device number of a printer (4 or 5)");
        if (systemRoms is null) throw new InvalidOperationException("The C64 is not running");
        if (printer is null) printer = new Mps803(systemRoms.Chargen, device);
        else printer.DeviceNumber = device;
        Rewire();
    }

    // What was printed, as a PDF
    public static byte[] PrinterPdf() => printer?.ToPdf() ?? [];

    // A new sheet of paper
    public static void ClearPrinter() => printer?.NewPaper();
}
