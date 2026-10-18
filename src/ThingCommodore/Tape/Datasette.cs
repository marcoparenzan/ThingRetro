using ThingRetroEmu.Ports;

namespace ThingCommodore.Tape;

// The keys of the recorder. Any key down pulls SENSE low, which is how the computer knows
public enum DatasetteKey { Stop, Play, Record, Rewind, FastForward }

/*
    The C2N (1530) cassette recorder, a stand-alone machine: a tape (a .tap image), a motor that the computer switches on and
    off, a read head that sends the pulses to the computer's READ line, a write head that listens to its WRITE line, and the keys.
    It knows nothing about the computer on the other side of the cable: it is plugged into a CassettePort, and the host calls
    Tick once for every cycle of the computer's clock.

    The motor runs only while the computer asks for it and a key is down. Playing sends a falling edge at the end of each
    pulse of the tape; recording measures the time between the rising edges of the WRITE line and appends it to the tape.
    Rewind and fast forward do not need the computer: they run as long as the key is down, at a speed of a few seconds a minute,
    here a pulse at a time (FastSpeed).
 */
public sealed class Datasette
{
    const int LOW_CYCLES = 60;          // how long READ stays low after the falling edge
    public const int FastSpeed = 200;   // pulses per cycle of the computer, rewinding and fast forwarding

    CassettePort? port;
    TapeCursor cursor = new();
    bool readHigh = true;
    int lowRemaining;
    int cyclesInPulse;
    bool lastWriteHigh = true;
    int sinceEdge;
    bool recordingStarted;

    public DatasetteKey Key { get; private set; } = DatasetteKey.Stop;

    // The tape in the recorder (null = no cassette)
    public ThingRetroEmu.Media.TapImage? Tape
    {
        get => cursor.Image;
        set
        {
            cursor = new TapeCursor { Image = value };
            cyclesInPulse = 0;
            recordingStarted = false;
        }
    }

    // Plugged into the computer's cassette connector
    public void Plug(CassettePort cassettePort)
    {
        port = cassettePort;
        cassettePort.SenseLow = () => Key != DatasetteKey.Stop;
        cassettePort.ReadHigh = () => readHigh;
    }

    public bool MotorOn => Key switch
    {
        DatasetteKey.Stop => false,
        DatasetteKey.Rewind or DatasetteKey.FastForward => true,
        _ => port?.MotorOn() == true,
    };

    // The number of the pulse under the head, and the position in seconds of a PAL machine, for the counter
    public int Position => cursor.Index;
    public double Seconds => cursor.ElapsedCycles / 985_248.0;
    public bool AtEnd => cursor.Image is null || cursor.Index >= cursor.Image.Count;
    public bool WriteProtected { get; set; }

    public void Press(DatasetteKey key)
    {
        if (key == DatasetteKey.Record && (Tape is null || WriteProtected)) return;   // RECORD does not go down on a protected tape
        if (key != DatasetteKey.Record) recordingStarted = false;
        Key = key;
        if (key == DatasetteKey.Record) recordingStarted = false;
    }

    public void Stop() => Press(DatasetteKey.Stop);
    public void Play() => Press(DatasetteKey.Play);
    public void Record() => Press(DatasetteKey.Record);
    public void Rewind() => Press(DatasetteKey.Rewind);
    public void FastForward() => Press(DatasetteKey.FastForward);

    // One cycle of the computer's clock
    public void Tick()
    {
        if (lowRemaining > 0 && --lowRemaining == 0) readHigh = true;
        if (Key == DatasetteKey.Stop || cursor.Image is null) return;

        switch (Key)
        {
            case DatasetteKey.Rewind:
                for (int i = 0; i < FastSpeed && cursor.Index > 0; i++) cursor.StepBack();
                if (cursor.Index == 0) Key = DatasetteKey.Stop;      // the key springs up at the start
                return;
            case DatasetteKey.FastForward:
                for (int i = 0; i < FastSpeed && !AtEnd; i++) cursor.StepForward();
                if (AtEnd) Key = DatasetteKey.Stop;
                return;
        }

        if (port is null || !port.MotorOn()) return;

        if (Key == DatasetteKey.Play) PlayCycle();
        else RecordCycle();
    }

    void PlayCycle()
    {
        if (AtEnd)
        {
            Key = DatasetteKey.Stop;
            return;
        }
        if (++cyclesInPulse < cursor.Image!.Pulses[cursor.Index]) return;
        cyclesInPulse = 0;
        cursor.StepForward();
        readHigh = false;
        lowRemaining = LOW_CYCLES;
    }

    void RecordCycle()
    {
        bool high = port!.WriteHigh();
        sinceEdge++;
        if (!lastWriteHigh && high)         // the KERNAL starts each pulse with the rising edge of WRITE
        {
            if (!recordingStarted)
            {
                // The first rising edge starts the recording at the position of the head: what was after it is erased
                recordingStarted = true;
                cursor.Image!.TruncateAt(cursor.Index);
            }
            else
            {
                cursor.Image!.Append(sinceEdge);
                cursor.StepForward();
            }
            sinceEdge = 0;
        }
        lastWriteHigh = high;
    }

    // Remembers where the head is on the tape
    sealed class TapeCursor
    {
        public ThingRetroEmu.Media.TapImage? Image;
        public int Index;
        public long ElapsedCycles;

        public void StepForward()
        {
            if (Image is null || Index >= Image.Count) return;
            ElapsedCycles += Image.Pulses[Index++];
        }

        public void StepBack()
        {
            if (Image is null || Index == 0) return;
            ElapsedCycles -= Image.Pulses[--Index];
        }
    }
}
