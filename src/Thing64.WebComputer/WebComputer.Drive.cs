using ThingCommodore.Drives;
using ThingRetroEmu.Buses;
using ThingRetroEmu.Media;
using Thing64.C64;

namespace Thing64.WebComputer;

// A 1541 on the IEC cable of the C64 in the browser: the drive board (ThingCommodore), the cable, the clock that makes it
// follow the computer cycle by cycle, and the disk in it. The drive runs inside the C64's frames, one of its cycles
// after almost every one of the computer's, so loading and saving through the KERNAL work as on the real machines.
// The 1541's ROM, like the C64's, is not part of the site: the page asks for it once. The disk is a .d64 file the
// page was given and hands back (Save disk), changed by whatever the program wrote
public static partial class WebComputer
{
    public const string DriveRomName = "drive1541";      // in the browser's storage: thing64.rom.drive1541

    static Board1541? drive;
    static DriveClock? driveClock;
    static D64Image? disk;
    static string? diskName;

    // Debug builds carry the ROM (it is in the roms folder of the repository), like the C64's
    public static byte[]? EmbeddedDriveRom()
    {
        using var stream = typeof(WebComputer).Assembly.GetManifestResourceStream("roms/c-drive-1541.rom");
        if (stream is null) return null;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    public static bool DriveOn => drive is not null;
    public static int DriveDevice => drive?.DeviceNumber ?? 0;
    public static bool HasDisk => disk is not null;
    public static string? DiskName => diskName;

    // True when the program wrote something to the disk since it was put in or saved
    public static bool DiskModified
    {
        get
        {
            drive?.Mechanism.Flush();
            return disk?.Modified == true;
        }
    }

    // Plugs a drive (device 8 to 11) into the cable; the disk that is in the page goes into it
    public static void AttachDrive(byte[] rom, int device)
    {
        if (board is null) throw new InvalidOperationException("The C64 is not running");
        if (rom.Length != Board1541.ROM_SIZE) throw new ArgumentException($"a 1541 ROM is {Board1541.ROM_SIZE} bytes, this file is {rom.Length}");
        if (device is < 8 or > 11) throw new ArgumentException($"{device} is not a device number of a drive (8 to 11)");
        DetachDrive();
        var unit = new Board1541(rom) { DeviceNumber = device };
        unit.Disk = disk;
        drive = unit;
        driveClock = new DriveClock(CyclesPerSecond, unit.Tick);
        Rewire();
    }

    // Takes the drive off the cable; the disk stays in the page (what the drive had written is in it)
    public static void DetachDrive()
    {
        if (drive is null) return;
        drive.Mechanism.Flush();
        drive.Unplug();
        drive = null;
        driveClock = null;
        Rewire();
    }

    // A .d64 in the drive (the one that was there goes out, what was written to it kept in its image). The size is
    // checked: 35 tracks, with or without the error bytes
    public static void InsertDisk(string name, byte[] content)
    {
        var image = new D64Image(content);
        drive?.Mechanism.Flush();
        disk = image;
        diskName = name;
        if (drive is not null) drive.Disk = image;
    }

    public static void EjectDisk()
    {
        drive?.Mechanism.Flush();
        if (drive is not null) drive.Disk = null;
        disk = null;
        diskName = null;
    }

    // The disk as it is now, to be saved as a file
    public static byte[] DiskBytes()
    {
        drive?.Mechanism.Flush();
        return disk?.Data ?? [];
    }

    public static void DiskSaved() => disk?.MarkSaved();

    // Describes the drive for the page: "no drive", "drive 8, no disk", "drive 8: name.d64 (changed)"
    public static string DriveText()
    {
        if (drive is null) return disk is null ? "no drive" : $"{diskName}: no drive";
        if (disk is null) return $"drive {drive.DeviceNumber}: no disk";
        return $"drive {drive.DeviceNumber}: {diskName}{(DiskModified ? " (changed)" : "")}";
    }
}
