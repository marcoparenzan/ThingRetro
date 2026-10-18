using ThingCommodore.Drives;
using ThingRetroEmu.Media;

namespace Thing64.WebComputer;

// A 1581 on the IEC cable of the C64 in the browser, next to (or instead of) the 1541: the drive board (ThingCommodore), a clock at
// 2 MHz that follows the computer cycle by cycle, and the .d81 disk in it. Its ROM (32 KB) is not part of the site either: the page
// asks for it once. The disk is a .d81 file the page was given and hands back (Save disk), changed by whatever the program wrote
public static partial class WebComputer
{
    public const string Drive1581RomName = "drive1581";     // in the browser's storage: thing64.rom.drive1581

    static Board1581? drive81;
    static DriveClock? drive81Clock;
    static D81Image? disk81;
    static string? disk81Name;

    // Debug builds carry the ROM (it is in the roms folder of the repository), like the C64's
    public static byte[]? EmbeddedDrive1581Rom()
    {
        using var stream = typeof(WebComputer).Assembly.GetManifestResourceStream("roms/c-drive-1581.rom");
        if (stream is null) return null;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    public static bool Drive81On => drive81 is not null;
    public static int Drive81Device => drive81?.DeviceNumber ?? 0;
    public static bool HasDisk81 => disk81 is not null;
    public static string? DiskName81 => disk81Name;
    public static bool Disk81Modified => disk81?.Modified == true;

    // Plugs a 1581 (device 8 to 11) into the cable; the disk that is in the page goes into it
    public static void AttachDrive81(byte[] rom, int device)
    {
        if (board is null) throw new InvalidOperationException("The C64 is not running");
        if (rom.Length != Board1581.ROM_SIZE) throw new ArgumentException($"a 1581 ROM is {Board1581.ROM_SIZE} bytes, this file is {rom.Length}");
        if (device is < 8 or > 11) throw new ArgumentException($"{device} is not a device number of a drive (8 to 11)");
        if (drive is not null && drive.DeviceNumber == device) throw new ArgumentException($"the 1541 is already device {device}");
        DetachDrive81();
        var unit = new Board1581(rom) { DeviceNumber = device };
        unit.Disk = disk81;
        drive81 = unit;
        drive81Clock = new DriveClock(CyclesPerSecond, unit.Tick, Board1581.CLOCK_HZ);
        Rewire();
    }

    // Takes the drive off the cable; the disk stays in the page (what the drive had written is in it)
    public static void DetachDrive81()
    {
        if (drive81 is null) return;
        drive81.Unplug();
        drive81 = null;
        drive81Clock = null;
        Rewire();
    }

    // A .d81 in the drive (the one that was there goes out, what was written to it kept in its image)
    public static void InsertDisk81(string name, byte[] content)
    {
        var image = new D81Image(content);
        disk81 = image;
        disk81Name = name;
        if (drive81 is not null) drive81.Disk = image;
    }

    public static void EjectDisk81()
    {
        if (drive81 is not null) drive81.Disk = null;
        disk81 = null;
        disk81Name = null;
    }

    // The disk as it is now, to be saved as a file
    public static byte[] Disk81Bytes() => disk81?.Data ?? [];

    public static void Disk81Saved() => disk81?.MarkSaved();

    // Describes the drive for the page: "no 1581", "1581 drive 9, no disk", "1581 drive 9: name.d81 (changed)"
    public static string Drive81Text()
    {
        if (drive81 is null) return disk81 is null ? "no 1581" : $"{disk81Name}: no 1581";
        if (disk81 is null) return $"1581 drive {drive81.DeviceNumber}: no disk";
        return $"1581 drive {drive81.DeviceNumber}: {disk81Name}{(Disk81Modified ? " (changed)" : "")}";
    }
}
