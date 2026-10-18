namespace ThingRetroEmu.Tests;

[TestClass]
public sealed class D64Tests
{
    [TestMethod]
    public void Image_ShouldHaveTheSizeAndZonesOfAnInchSeventyFiveDisk()
    {
        Assert.AreEqual(683, Enumerable.Range(1, 35).Sum(D64Image.SectorsPerTrack));
        Assert.AreEqual(21, D64Image.SectorsPerTrack(1));
        Assert.AreEqual(19, D64Image.SectorsPerTrack(18));
        Assert.AreEqual(18, D64Image.SectorsPerTrack(25));
        Assert.AreEqual(17, D64Image.SectorsPerTrack(35));
        Assert.AreEqual(7692, D64Image.TrackBytes(1));
        Assert.AreEqual(6250, D64Image.TrackBytes(35));
        Assert.ThrowsExactly<InvalidDataException>(() => new D64Image(new byte[1000]));
    }

    [TestMethod]
    public void Sectors_ShouldBeWrittenAndReadAtTheirPlace()
    {
        var disk = D64Image.Blank();
        var data = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

        Assert.IsTrue(disk.WriteSector(18, 1, data));

        var back = new byte[256];
        Assert.IsTrue(disk.ReadSector(18, 1, back));
        CollectionAssert.AreEqual(data, back);
        Assert.AreEqual(data[5], disk.Data[(17 * 21 + 1) * 256 + 5], "track 18 starts after 17 tracks of 21 sectors");
        Assert.IsFalse(disk.ReadSector(18, 19, back));
        Assert.IsFalse(disk.ReadSector(36, 0, back));
        Assert.IsTrue(disk.Modified);
    }

    [TestMethod]
    public void WriteProtectedDisk_ShouldRefuseWrites()
    {
        var disk = D64Image.Blank();
        disk.WriteProtected = true;

        Assert.IsFalse(disk.WriteSector(1, 0, new byte[256]));
    }

    [TestMethod]
    public void Writer_ShouldMakeADirectoryAndTheBam()
    {
        var disk = D64Writer.Create("MY DISK", [("PROG", new byte[] { 1, 8, 1, 2, 3 })]);
        var bam = new byte[256];
        var directory = new byte[256];
        disk.ReadSector(18, 0, bam);
        disk.ReadSector(18, 1, directory);

        Assert.AreEqual((byte)'M', bam[0x90]);
        Assert.AreEqual((byte)'6', bam[0xA2]);
        Assert.AreEqual(21 - 1, bam[4], "track 1 lost the sector of the file");
        Assert.AreEqual(19 - 2, bam[4 * 18], "the directory track lost the BAM and the directory");
        Assert.AreEqual(0x82, directory[2]);
        Assert.AreEqual((byte)'P', directory[5]);
        Assert.AreEqual(1, directory[3], "the file starts at track 1");
    }

    [TestMethod]
    public void Gcr_ShouldHaveSyncBeforeEveryBlockAndTheHeaderOfEverySector()
    {
        var disk = D64Writer.Create("X", [("A", new byte[] { 1, 8 })]);
        var track = GcrTrack.Encode(disk, 18);

        Assert.AreEqual(D64Image.TrackBytes(18), track.Length);
        int syncRuns = 0;
        for (int i = 0; i < track.Length; i++)
            if (track.Sync[i] && (i == 0 || !track.Sync[i - 1])) syncRuns++;
        Assert.AreEqual(2 * 19, syncRuns, "two per sector: header and data");
        Assert.IsTrue(Enumerable.Range(0, track.Length).Where(i => track.Sync[i]).All(i => track.Bytes[i] == 0xFF));
        Assert.IsFalse(Enumerable.Range(0, track.Length).Where(i => !track.Sync[i]).Any(i => track.Bytes[i] == 0xFF && track.Bytes[(i + 1) % track.Length] == 0xFF),
            "two $FF in a row are SYNC and nothing else");
    }

    [TestMethod]
    public void Gcr_HeaderShouldDecodeToTheSectorAndTrack()
    {
        var disk = D64Writer.Create("X", []);
        var track = GcrTrack.Encode(disk, 5);

        // The first header: after the 5 SYNC bytes, 10 bytes of GCR for 08 checksum sector track id2 id1 0F 0F
        var decoded = Decode(track.Bytes.AsSpan(5, 10));
        Assert.AreEqual(0x08, decoded[0]);
        Assert.AreEqual(0, decoded[2], "sector 0");
        Assert.AreEqual(5, decoded[3], "track 5");
        Assert.AreEqual(decoded[1], (byte)(decoded[2] ^ decoded[3] ^ decoded[4] ^ decoded[5]), "checksum");
    }

    [TestMethod]
    public void Decode_ShouldGiveBackTheSectorsOfTheTrack()
    {
        var source = D64Writer.Create("X", [("A", Enumerable.Range(0, 700).Select(i => (byte)(i * 3)).ToArray())]);
        var track = GcrTrack.Encode(source, 1);
        var target = D64Writer.Create("X", []);

        int found = track.Decode(target, 1);

        Assert.AreEqual(21, found);
        var a = new byte[256];
        var b = new byte[256];
        for (int sector = 0; sector < 21; sector++)
        {
            source.ReadSector(1, sector, a);
            target.ReadSector(1, sector, b);
            CollectionAssert.AreEqual(a, b, $"sector {sector}");
        }
    }

    [TestMethod]
    public void Decode_ShouldIgnoreASectorWithABadChecksum()
    {
        var source = D64Writer.Create("X", []);
        var data = Enumerable.Repeat((byte)0x11, 256).ToArray();
        source.WriteSector(1, 3, data);
        var track = GcrTrack.Encode(source, 1);
        // Spoil one data byte of sector 3: after its second SYNC, a few bytes in
        int sync = 0, at = 0;
        for (int i = 1; i < track.Length && sync < 8; i++)
            if (track.Sync[i - 1] && !track.Sync[i] && ++sync == 8) at = i;     // 4th sector, data block
        track.Bytes[at + 40] ^= 0x01;
        var target = D64Writer.Create("X", []);
        var untouched = Enumerable.Repeat((byte)0x77, 256).ToArray();
        target.WriteSector(1, 3, untouched);

        track.Decode(target, 1);

        var back = new byte[256];
        target.ReadSector(1, 3, back);
        CollectionAssert.AreEqual(untouched, back);
    }

    [TestMethod]
    public void Decode_ShouldGiveBackAnyData()
    {
        var random = new Random(65);
        var source = D64Writer.Create("X", []);
        for (int sector = 0; sector < 21; sector++)
        {
            var data = new byte[256];
            random.NextBytes(data);
            if (sector % 3 == 0) Array.Fill(data, (byte)0xFF, 10, 40);   // lots of ones
            source.WriteSector(3, sector, data);
        }
        var track = GcrTrack.Encode(source, 3);
        track.FindSync();
        var target = D64Writer.Create("X", []);

        Assert.AreEqual(21, track.Decode(target, 3));

        var a = new byte[256];
        var b = new byte[256];
        for (int sector = 0; sector < 21; sector++)
        {
            source.ReadSector(3, sector, a);
            target.ReadSector(3, sector, b);
            CollectionAssert.AreEqual(a, b, $"sector {sector}");
        }
    }

    static readonly byte[] Codes = [0x0A, 0x0B, 0x12, 0x13, 0x0E, 0x0F, 0x16, 0x17, 0x09, 0x19, 0x1A, 0x1B, 0x0D, 0x1D, 0x1E, 0x15];

    static byte[] Decode(ReadOnlySpan<byte> gcr)
    {
        var result = new byte[gcr.Length / 5 * 4];
        for (int g = 0, o = 0; g < gcr.Length; g += 5, o += 4)
        {
            ulong bits = 0;
            for (int i = 0; i < 5; i++) bits = bits << 8 | gcr[g + i];
            for (int n = 0; n < 4; n++)
            {
                int high = Array.IndexOf(Codes, (byte)(bits >> (35 - 10 * n) & 0x1F));
                int low = Array.IndexOf(Codes, (byte)(bits >> (30 - 10 * n) & 0x1F));
                Assert.IsTrue(high >= 0 && low >= 0, "not a GCR code");
                result[o + n] = (byte)(high << 4 | low);
            }
        }
        return result;
    }
}
