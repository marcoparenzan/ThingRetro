namespace ThingMOS.Tests;

// The F011's free-format track commands, driven the way a DOS does it: byte by byte through the data and clock registers.
// The revolution is shortened (40 cycles per byte) so the tests are quick
[TestClass]
public sealed class F011TracksTests
{
    const int RevolutionCycles = 250_000; // 6250 bytes of 40 cycles

    // The F011's own CRC: all ones, polynomial $1021, over the marks and the field
    static ushort Crc(IEnumerable<byte> bytes)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in bytes)
        {
            crc ^= (ushort)(b << 8);
            for (int bit = 0; bit < 8; bit++) crc = (ushort)((crc & 0x8000) != 0 ? crc << 1 ^ 0x1021 : crc << 1);
        }
        return crc;
    }

    static F011 NewController(IDiskMedia media)
    {
        var fdc = new F011 { Media = media, RevolutionCycles = RevolutionCycles };
        fdc.Write(0, 0x28);            // side 0, drive 0
        Settle(fdc);
        return fdc;
    }

    // A write to the control register keeps BUSY set for a moment, and a command written then is ignored: a DOS waits
    static void Settle(F011 fdc)
    {
        for (int i = 0; i < 50; i++) fdc.Tick();
    }

    // A byte of the stream and whether it is a mark (A1 with the clock FB)
    static List<(byte Data, bool Mark)> Stream(params (int Sector, byte Fill, bool BadCrc)[] sectors) => StreamOnSide(0, sectors);

    // The side number goes in the header: it is what names the sector, whatever side the head is on
    static List<(byte Data, bool Mark)> StreamOnSide(byte side, params (int Sector, byte Fill, bool BadCrc)[] sectors)
    {
        var bytes = new List<(byte, bool)>();
        void Gap(int count, byte value) { for (int i = 0; i < count; i++) bytes.Add((value, false)); }
        Gap(60, 0x4E);
        foreach (var (sector, fill, badCrc) in sectors)
        {
            Gap(12, 0x00);
            var header = new List<byte> { 0xA1, 0xA1, 0xA1, 0xFE, 0, side, (byte)sector, 2 };
            for (int i = 0; i < 3; i++) bytes.Add((0xA1, true));
            foreach (byte b in header.Skip(3)) bytes.Add((b, false));
            ushort crc = Crc(header);
            bytes.Add(((byte)(crc >> 8), false)); bytes.Add(((byte)crc, false));
            Gap(22, 0x4E);
            Gap(12, 0x00);
            var field = new List<byte> { 0xA1, 0xA1, 0xA1, 0xFB };
            field.AddRange(Enumerable.Repeat(fill, 512));
            for (int i = 0; i < 3; i++) bytes.Add((0xA1, true));
            foreach (byte b in field.Skip(3)) bytes.Add((b, false));
            crc = Crc(field);
            if (badCrc) crc ^= 0x1234;
            bytes.Add(((byte)(crc >> 8), false)); bytes.Add(((byte)crc, false));
            Gap(24, 0x4E);
        }
        Gap(F011.TRACK_BYTES - bytes.Count, 0x4E);
        return bytes;
    }

    // Issues WTT (unbuffered) and feeds the stream: for each byte, the clock, then the data when DRQ says the previous one is on the disk
    static void WriteTrack(F011 fdc, List<(byte Data, bool Mark)> stream, int stopAfter = int.MaxValue)
    {
        fdc.Write(8, 0xFF);
        fdc.Write(7, 0xFF);
        fdc.Write(1, 0xA1);
        int index = 0;
        int guard = 0;
        while ((fdc.Read(2) & 0x80) != 0 || guard++ < 5000)
        {
            fdc.Tick();
            if ((fdc.Read(2) & 0x40) != 0 && index < stream.Count && index < stopAfter)
            {
                fdc.Write(8, stream[index].Mark ? (byte)0xFB : (byte)0xFF);
                fdc.Write(7, stream[index].Data);
                index++;
            }
            if (guard > 5_000_000) Assert.Fail("the command does not end");
        }
    }

    [TestMethod]
    public void WriteTrack_ShouldCreateTheSectorsOfTheStream()
    {
        var disk = D81Image.Blank();
        var fdc = NewController(disk);
        var buffer = new byte[512];
        Assert.IsFalse(disk.ReadSector(0, 0, 1, buffer), "an unformatted track has no sectors");

        WriteTrack(fdc, Stream((1, 0xAA, false), (2, 0x55, false), (3, 0x77, false)));

        Assert.IsTrue(disk.ReadSector(0, 0, 1, buffer));
        Assert.IsTrue(buffer.All(b => b == 0xAA));
        Assert.IsTrue(disk.ReadSector(0, 0, 2, buffer));
        Assert.IsTrue(buffer.All(b => b == 0x55));
        Assert.IsTrue(disk.ReadSector(0, 0, 3, buffer));
        Assert.IsFalse(disk.ReadSector(0, 0, 4, buffer), "not in the stream");
        Assert.IsFalse(disk.ReadSector(0, 1, 1, buffer), "the other side");
        Assert.IsFalse(disk.ReadSector(1, 0, 1, buffer), "the next track");
        Assert.AreEqual(0, fdc.Read(2) & 0x80, "BUSY ends with the revolution");
        Assert.AreNotEqual(0, fdc.Read(3) & 0x02, "IRQ");
    }

    [TestMethod]
    public void WriteTrack_ShouldErasePreviousData()
    {
        var disk = new D81Image(new byte[D81Image.SIZE]);
        disk.WriteSector(0, 0, 9, Enumerable.Repeat((byte)0x99, 512).ToArray());
        var fdc = NewController(disk);

        WriteTrack(fdc, Stream((1, 0x11, false)));

        var buffer = new byte[512];
        Assert.IsFalse(disk.ReadSector(0, 0, 9, buffer), "formatting destroyed it");
        Assert.IsTrue(disk.ReadSector(0, 0, 1, buffer));
    }

    [TestMethod]
    public void WriteTrack_WithABadCrc_ShouldNotCreateTheSector()
    {
        var disk = D81Image.Blank();
        var fdc = NewController(disk);

        WriteTrack(fdc, Stream((1, 0x11, true), (2, 0x22, false)));

        var buffer = new byte[512];
        Assert.IsFalse(disk.ReadSector(0, 0, 1, buffer));
        Assert.IsTrue(disk.ReadSector(0, 0, 2, buffer));
    }

    [TestMethod]
    public void WriteTrack_ShouldWriteTheSideSelected()
    {
        var disk = D81Image.Blank();
        var fdc = NewController(disk);
        fdc.Write(0, 0x20);            // the SIDE bit clear: side 1
        Settle(fdc);

        WriteTrack(fdc, StreamOnSide(1, (1, 0x33, false)));

        var buffer = new byte[512];
        Assert.IsTrue(disk.ReadSector(0, 1, 1, buffer));
        Assert.IsFalse(disk.ReadSector(0, 0, 1, buffer));
    }

    [TestMethod]
    public void WriteTrack_WhenCancelled_ShouldLeaveTheDisk()
    {
        var disk = new D81Image(new byte[D81Image.SIZE]);
        var fdc = NewController(disk);
        fdc.Write(8, 0xFF);
        fdc.Write(7, 0x4E);
        fdc.Write(1, 0xA1);
        for (int i = 0; i < RevolutionCycles / 2; i++) fdc.Tick();
        Assert.AreNotEqual(0, fdc.Read(2) & 0x80);

        fdc.Write(1, 0x00); // CAN

        Assert.AreEqual(0, fdc.Read(2) & 0x80);
        var buffer = new byte[512];
        Assert.IsTrue(disk.ReadSector(0, 0, 1, buffer), "nothing was erased");
    }

    [TestMethod]
    public void WriteTrack_WithALateCpu_ShouldSetLost()
    {
        var disk = D81Image.Blank();
        var fdc = NewController(disk);
        fdc.Write(8, 0xFF);
        fdc.Write(7, 0x4E);
        fdc.Write(1, 0xA1);

        for (int i = 0; i < RevolutionCycles + 300; i++) fdc.Tick(); // the CPU supplies nothing more

        Assert.AreNotEqual(0, fdc.Read(2) & 0x04, "LOST");
    }

    // A track read back: RDT unbuffered gives the stream of the manual's layout, with the right CRCs
    [TestMethod]
    public void ReadTrack_ShouldGiveTheFormattedLayoutOfTheSectors()
    {
        var disk = new D81Image(new byte[D81Image.SIZE]);
        disk.WriteSector(0, 0, 1, Enumerable.Repeat((byte)0xC3, 512).ToArray());
        var fdc = NewController(disk);

        fdc.Write(1, 0x61); // RDT, unbuffered
        var bytes = new List<byte>();
        for (int guard = 0; bytes.Count < 700; guard++)
        {
            Assert.IsTrue(guard < 1_000_000, "the track read does not deliver bytes");
            fdc.Tick();
            if ((fdc.Read(2) & 0x40) != 0) bytes.Add(fdc.Read(7));
        }
        fdc.Write(1, 0x00);

        Assert.IsTrue(bytes.Take(60).All(b => b == 0x4E), "the gap");
        int mark = bytes.FindIndex(b => b == 0xA1);
        Assert.AreEqual(60 + 12, mark);
        CollectionAssert.AreEqual(new byte[] { 0xA1, 0xA1, 0xA1, 0xFE, 0, 0, 1, 2 }, bytes.GetRange(mark, 8));
        ushort crc = Crc(bytes.GetRange(mark, 8));
        Assert.AreEqual(crc, (ushort)(bytes[mark + 8] << 8 | bytes[mark + 9]), "the CRC of the header");
        int data = bytes.FindIndex(mark + 10, b => b == 0xFB);
        Assert.IsTrue(bytes.GetRange(data + 1, 512).All(b => b == 0xC3));
        Assert.AreEqual(0, fdc.Read(2) & 0x80, "cancelled");
    }

    [TestMethod]
    public void Format_ThenRead_ShouldRoundTripThroughTheSectorCommands()
    {
        var disk = D81Image.Blank();
        var fdc = NewController(disk);
        WriteTrack(fdc, Stream((1, 0xAB, false)));

        // A sector read of what was formatted
        fdc.Write(4, 0);
        fdc.Write(5, 1);
        fdc.Write(6, 0);
        fdc.Write(1, 0x40);
        for (int i = 0; i < 3000; i++) fdc.Tick();

        Assert.AreEqual(0, fdc.Read(2) & 0x10, "found");
        Assert.AreEqual(0xAB, fdc.Read(7));
    }
}
