using ThingCommodore.Drives;
using ThingRetroEmu.Buses;
using ThingRetroEmu.Media;
using Thing65.C65;

namespace Thing65.Computer;

// A 1541 on the IEC cable of the C65: the drive board, the cable, the clock that makes it follow the computer, and the
// disk in it with the file it came from (written back when the disk is taken out, replaced, or the window closes).
// The drive runs on the emulation thread, at the end of every cycle of the board: whatever changes it is sent there with
// Enqueue
public sealed class Drive1541Host
{
    public const string RomName = "c-drive-1541.rom";

    readonly BoardC65 board;
    readonly Board1541 drive;

    public Drive1541Host(BoardC65 board, byte[] rom, int deviceNumber)
    {
        this.board = board;
        drive = new Board1541(rom) { DeviceNumber = deviceNumber };
        var cable = new IecBus();
        cable.Attach(() => board.Iec.AtnLow(), () => board.Iec.ClkLow(), () => board.Iec.DataLow(),
            input => board.Iec.ClkInputLow = input, input => board.Iec.DataInputLow = input);
        drive.Plug(cable);
        var clock = new DriveClock(985_248, drive.Tick);
        board.Enqueue(() => board.AfterCycle = clock.OnComputerCycle);
    }

    public Mechanism1541 Mechanism => drive.Mechanism;
    public int DeviceNumber => drive.DeviceNumber;
    public string? Path { get; private set; }
    public D64Image? Image => drive.Disk;
    public bool Modified => drive.Disk?.Modified == true;

    // The drive is taken off the cable: it stops, and the disk is saved
    public void Detach()
    {
        board.Enqueue(() =>
        {
            board.AfterCycle = null;
            Save();
        });
    }

    public void Eject() => Insert(null, null);

    public void RequestSave() => board.Enqueue(() => Save());

    // Puts a disk in (the one that was in is saved first); null = no disk. Runs on the emulation thread
    public void Insert(D64Image? image, string? path)
    {
        board.Enqueue(() =>
        {
            Save();
            drive.Mechanism.Flush();
            drive.Disk = image;
            Path = path;
        });
    }

    // Writes the disk to its file if it was changed and has one. On the emulation thread
    public bool Save()
    {
        drive.Mechanism.Flush();
        if (drive.Disk is not { Modified: true } disk || Path is null || disk.WriteProtected) return false;
        File.WriteAllBytes(Path, disk.Data);
        disk.MarkSaved();
        return true;
    }

    public void SaveAs(string path)
    {
        if (drive.Disk is not { } disk) throw new InvalidOperationException("There is no disk in the drive");
        drive.Mechanism.Flush();
        File.WriteAllBytes(path, disk.Data);
        disk.MarkSaved();
        Path = path;
    }

    public static string? FindRom(string? folder = null)
    {
        folder ??= RomFolder.DefaultPath;
        string file = System.IO.Path.Combine(folder, RomName);
        return File.Exists(file) && new FileInfo(file).Length == Board1541.ROM_SIZE ? file : null;
    }
}
