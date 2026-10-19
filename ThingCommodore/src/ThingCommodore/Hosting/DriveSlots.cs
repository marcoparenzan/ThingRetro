using ThingCommodore.Drives;
using ThingRetroEmu.Buses;
using ThingRetroEmu.Media;

namespace ThingCommodore.Hosting;

/// <summary>
/// A place for a disk drive in the rack: the drive (plugged in as a device number), the disk in it and the file the disk came from. The disk is written back to its
/// file on request (<see cref="Save"/>), when it is taken out, replaced or the program ends. Reading the properties is safe from any thread
/// </summary>
public abstract class DriveSlot<TImage> where TImage : class
{
    protected readonly PeripheralRack Rack;
    int device;
    TImage? image;
    string? path;

    protected DriveSlot(PeripheralRack rack) => Rack = rack;

    /// <summary>"1541" or "1581".</summary>
    public abstract string Model { get; }
    protected abstract string RomName { get; }
    protected abstract string Extension { get; }
    protected abstract string[] Devices { get; }

    /// <summary>The device number (8-11) the drive answers to, or 0 if it is not on the cable.</summary>
    public int Device => device;
    public bool HasDisk => image is not null;
    public string? Path => path;
    public string DiskName => image is null ? "no disk" : (path is null ? "(not saved)" : System.IO.Path.GetFileName(path));
    public abstract bool DiskModified { get; }
    public bool RomAvailable => Rack.Rom(RomName) is not null;
    public string RomFileName => RomName;

    protected TImage? Image => image;

    protected abstract TImage Parse(byte[] content);
    protected abstract bool PlugIn(int deviceNumber, byte[] rom);
    protected abstract void Unplug();
    protected abstract void SetDisk(TImage? disk);
    protected abstract byte[] DiskBytes();
    protected abstract void MarkSaved();
    protected abstract void FlushMechanism();
    protected abstract Action? ConnectDrive(IecBus cable);

    internal Action? Connect(IecBus cable) => device == 0 ? null : ConnectDrive(cable);

    /// <summary>Plugs the drive in as this device (8-11). False if its ROM is not in the roms folder.</summary>
    public bool Attach(int deviceNumber)
    {
        if (deviceNumber is < 8 or > 11) throw new ArgumentOutOfRangeException(nameof(deviceNumber), "The device number is 8 to 11");
        var rom = Rack.Rom(RomName);
        if (rom is null) return false;
        device = deviceNumber;
        Rack.Enqueue(() =>
        {
            if (!PlugIn(deviceNumber, rom)) return;
            SetDisk(image);
            Rack.Rewire();
        });
        return true;
    }

    /// <summary>Takes the drive off the cable; the disk stays in the slot.</summary>
    public void Detach()
    {
        device = 0;
        Rack.Enqueue(() =>
        {
            WriteBack();
            Unplug();
            Rack.Rewire();
        });
    }

    /// <summary>Puts the disk of a file in (the drive stays unplugged if it is not). Throws if the file is not a disk of this kind.</summary>
    public void Insert(string file)
    {
        var disk = Parse(File.ReadAllBytes(file));
        Rack.Enqueue(() =>
        {
            WriteBack();
            image = disk;
            path = file;
            SetDisk(disk);
        });
    }

    public void Eject() => Rack.Enqueue(() =>
    {
        WriteBack();
        image = null;
        path = null;
        SetDisk(null);
    });

    /// <summary>Writes the disk to the file it came from, if it was changed and has one.</summary>
    public void Save() => Rack.Enqueue(WriteBack);

    /// <summary>Writes the disk to a file, which becomes its file.</summary>
    public void SaveAs(string file, Action<Exception>? failed = null) => Rack.Enqueue(() =>
    {
        if (image is null) return;
        try
        {
            FlushMechanism();
            File.WriteAllBytes(file, DiskBytes());
            MarkSaved();
            path = file;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { failed?.Invoke(e); }
    });

    /// <summary>Writes the disk back now, on the calling thread (for when the board is stopped).</summary>
    public void SaveNow() => WriteBack();

    void WriteBack()
    {
        if (image is null || path is null) return;
        FlushMechanism();
        if (!DiskModified) return;
        File.WriteAllBytes(path, DiskBytes());
        MarkSaved();
    }
}

public sealed class DriveSlot1541(PeripheralRack rack) : DriveSlot<D64Image>(rack)
{
    Board1541? board;
    DriveClock? clock;

    public override string Model => "1541";
    protected override string RomName => "c-drive-1541.rom";
    protected override string Extension => ".d64";
    protected override string[] Devices => ["8", "9", "10", "11"];

    /// <summary>The mechanics (LED, motor, head) for a window that shows them.</summary>
    public Mechanism1541? Mechanism => board?.Mechanism;

    public override bool DiskModified => Image?.Modified == true;

    protected override D64Image Parse(byte[] content) => new(content);

    protected override bool PlugIn(int deviceNumber, byte[] rom)
    {
        if (board is null) board = new Board1541(rom);
        board.DeviceNumber = deviceNumber;
        clock = new DriveClock(PeripheralRack.ComputerHz, board.Tick);
        return true;
    }

    protected override void Unplug()
    {
        board?.Unplug();
        clock = null;
    }

    protected override void SetDisk(D64Image? disk)
    {
        if (board is null) return;
        board.Mechanism.Flush();
        board.Disk = disk;
    }

    protected override byte[] DiskBytes() => Image!.Data;
    protected override void MarkSaved() => Image?.MarkSaved();
    protected override void FlushMechanism() => board?.Mechanism.Flush();

    protected override Action? ConnectDrive(IecBus cable)
    {
        if (board is null || clock is null) return null;
        board.Unplug();
        board.Plug(cable);
        return clock.OnComputerCycle;
    }
}

public sealed class DriveSlot1581(PeripheralRack rack) : DriveSlot<D81Image>(rack)
{
    Board1581? board;
    DriveClock? clock;

    public override string Model => "1581";
    protected override string RomName => "c-drive-1581.rom";
    protected override string Extension => ".d81";
    protected override string[] Devices => ["8", "9", "10", "11"];

    public override bool DiskModified => Image?.Modified == true;

    protected override D81Image Parse(byte[] content) => new(content);

    protected override bool PlugIn(int deviceNumber, byte[] rom)
    {
        if (board is null) board = new Board1581(rom);
        board.DeviceNumber = deviceNumber;
        clock = new DriveClock(PeripheralRack.ComputerHz, board.Tick, Board1581.CLOCK_HZ);
        return true;
    }

    protected override void Unplug()
    {
        board?.Unplug();
        clock = null;
    }

    protected override void SetDisk(D81Image? disk)
    {
        if (board is not null) board.Disk = disk;
    }

    protected override byte[] DiskBytes() => Image!.Data;
    protected override void MarkSaved() => Image?.MarkSaved();
    protected override void FlushMechanism() => board?.Mechanism.Flush();

    protected override Action? ConnectDrive(IecBus cable)
    {
        if (board is null || clock is null) return null;
        board.Unplug();
        board.Plug(cable);
        return clock.OnComputerCycle;
    }
}
