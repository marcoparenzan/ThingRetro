namespace ThingCommodore.Drives;

// The mechanics of the 1541: the spindle motor, the stepper that moves the head, the read head with its GCR decoder front
// end. It turns the disk at 300 rpm and hands the VIA the bytes that pass under the head (one every 26 to 32 cycles
// according to the speed zone), a SYNC signal and a "byte ready" pulse. Writing: with the write gate on, the byte the VIA
// outputs replaces the one under the head at each byte time (and the byte ready pulse asks for the next). The track written
// is turned back into sectors of the disk image when the head stops writing, moves or the disk is taken out.
public sealed class Mechanism1541
{
    const int MAX_HALF_TRACK = 83;
    const int READY_PULSE_CYCLES = 3;
    const int REFRESH_CYCLES = 600_000;   // three turns

    D64Image? disk;
    GcrTrack? track;
    int trackNumber;
    int halfTrack;
    int position;
    int byteCycles;
    int readyCountdown;
    bool writing;
    bool trackChanged;

    public bool Motor { get; private set; }
    public bool Led { get; private set; }
    public int Density { get; private set; }

    public D64Image? Disk
    {
        get => disk;
        set
        {
            Flush();
            disk = value;
            trackNumber = 0;   // the head's track image is made again
        }
    }

    // The head in half tracks: track 1 is 0, track 2 is 2; odd values are between two tracks
    public int HalfTrack => halfTrack;

    // The byte under the head. SYNC is true (the line is low) while the head is over a SYNC mark
    public byte Data { get; private set; }
    public bool Sync { get; private set; }

    // True during the pulse that tells a byte is ready (the line is low then)
    public bool ByteReady => readyCountdown > 0;

    // The write protect sensor: a disk that is not protected lets the light through
    public bool WriteProtectSense => disk is not null && !disk.WriteProtected;

    // True while the write gate is on
    public bool Writing => writing;

    // The VIA's outputs: stepper phase (0-3) and whether the VIA drives it, motor, LED, speed zone, write gate (CB2 low) and the byte on port A
    public void Control(int phase, bool phaseDriven, bool motor, bool led, int density, bool write, byte data)
    {
        // The rotor sits at the phase that matches the head (phase 0 on the outer stop); a field one step on pulls it one half track,
        // unless the stop is in the way: then the rotor stays where it is. A field two steps away pulls it nowhere in particular
        if (phaseDriven)
        {
            int delta = (phase & 3) - (halfTrack & 3) & 3;
            if (delta == 1 && halfTrack < MAX_HALF_TRACK || delta == 3 && halfTrack > 0)
            {
                Flush();   // the head leaves the track
                halfTrack += delta == 1 ? 1 : -1;
            }
        }
        if (writing && !write) Flush();
        writing = write && motor && disk is { WriteProtected: false };
        writeData = data;
        Motor = motor;
        Led = led;
        Density = density & 3;
    }

    byte writeData;
    int refreshCountdown;

    // Bytes written to the disk since the start (for tools and tests)
    public long BytesWritten { get; private set; }

    // The track written is decoded into the sectors of the disk
    public void Flush()
    {
        if (trackChanged && disk is not null && track is not null)
        {
            track.FindSync();
            track.Decode(disk, trackNumber);
            // What the DOS wrote stays on the track for a while (the format routine times a turn by the SYNC it has just written);
            // then the track is made again from the disk image
            refreshCountdown = REFRESH_CYCLES;
   // what the head reads from now on is made from the disk image again
        }
        trackChanged = false;
    }

    public void Reset()
    {
        Flush();
        writing = false;
        halfTrack = 0;
        position = byteCycles = readyCountdown = 0;
        Motor = Led = Sync = false;
        Data = 0;
    }

    // One cycle (1 MHz)
    public void Tick()
    {
        if (readyCountdown > 0) readyCountdown--;
        if (refreshCountdown > 0 && !writing && --refreshCountdown == 0) trackNumber = 0;
        if (!Motor || disk is null)
        {
            Sync = false;
            return;
        }

        if (++byteCycles < 32 - 2 * Density) return;
        byteCycles = 0;

        int number = (halfTrack & 1) == 0 ? halfTrack / 2 + 1 : 0;   // between two tracks nothing can be read
        if (number is < 1 or > D64Image.TRACKS)
        {
            Sync = false;
            Data = 0;
            return;
        }
        if (number != trackNumber)
        {
            Flush();
            track = GcrTrack.Encode(disk, number);
            trackNumber = number;
        }

        position = (position + 1) % track!.Length;
        if (writing)
        {
            track.Bytes[position] = writeData;
            // two $FF in a row are SYNC; a single one is data
            track.Sync[position] = writeData == 0xFF && track.Bytes[(position + track.Length - 1) % track.Length] == 0xFF;
            if (track.Sync[position]) track.Sync[(position + track.Length - 1) % track.Length] = true;
            trackChanged = true;
            refreshCountdown = 0;
            BytesWritten++;
            Sync = false;
            readyCountdown = READY_PULSE_CYCLES;
            return;
        }
        Sync = track.Sync[position];
        Data = track.Bytes[position];
        if (!Sync) readyCountdown = READY_PULSE_CYCLES;
    }
}
