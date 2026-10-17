using System.Runtime.InteropServices;
using Thing65.C65;

namespace Thing65.WinForms;

// The PC speakers: an ISpeaker that plays the samples with the Windows audio API (waveOut, winmm).
// It is connected with an AudioCable (new AudioCable(board.AudioOut, speakers)).
// The emulation keeps pace with the PC clock, the sound card with its own: the two clocks differ slightly and
// the queue towards the card slowly grows or shrinks. To keep it at the wanted latency a block loses or repeats
// one sample (0.2%: inaudible) instead of skipping or repeating a whole block
public sealed unsafe class PcSpeakers : ISpeaker, IDisposable
{
    const int BUFFERS = 16;             // at most 160 ms of queue
    const int TARGET_QUEUED = 5;        // blocks queued in normal conditions: ~50 ms of latency

    readonly object gate = new();
    readonly IntPtr device;
    readonly WaveHeader*[] headers = new WaveHeader*[BUFFERS];
    readonly int capacity;              // samples per buffer
    bool paused;
    bool disposed;

    public int SampleRate { get; }

    // When muted the queue keeps flowing (silently): unmuting has no delay
    public bool Muted { get; set; }

    // Blocks dropped because the queue was full, and times it ran dry (the emulation fell behind)
    public int Overruns { get; private set; }
    public int Underruns { get; private set; }

    public PcSpeakers(int sampleRate = 44100)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("PcSpeakers uses the Windows audio");

        SampleRate = sampleRate;
        var format = new WaveFormat
        {
            FormatTag = WAVE_FORMAT_PCM,
            Channels = 1,
            SamplesPerSec = (uint)sampleRate,
            AvgBytesPerSec = (uint)sampleRate * 2,
            BlockAlign = 2,
            BitsPerSample = 16,
        };
        uint result = waveOutOpen(out device, WAVE_MAPPER, ref format, IntPtr.Zero, IntPtr.Zero, CALLBACK_NULL);
        if (result != 0) throw new InvalidOperationException($"Cannot open the audio device (waveOut error {result})");

        // Blocks of up to 40 ms: AudioPort sends 10 ms ones, plus the sample added to catch up
        capacity = sampleRate / 25;
        for (int i = 0; i < BUFFERS; i++)
        {
            headers[i] = (WaveHeader*)NativeMemory.AllocZeroed((nuint)sizeof(WaveHeader));
            headers[i]->Data = (IntPtr)NativeMemory.AllocZeroed((nuint)(capacity * sizeof(short)));
        }

        // The device starts paused: it begins playing when the queue is full enough
        waveOutPause(device);
        paused = true;
    }

    public void Play(ReadOnlySpan<short> samples)
    {
        lock (gate)
        {
            if (disposed || samples.IsEmpty) return;

            int queued = 0;
            WaveHeader* free = null;
            foreach (var header in headers)
            {
                if ((Volatile.Read(ref header->Flags) & WHDR_INQUEUE) != 0) queued++;
                else if (free == null) free = header;
            }

            if (queued == 0 && !paused)
            {
                // The queue ran dry: it fills up again before restarting, instead of stuttering
                waveOutPause(device);
                paused = true;
                Underruns++;
            }
            if (free is null)
            {
                Overruns++;
                return;
            }

            int length = Math.Min(samples.Length, capacity - 1);
            var buffer = new Span<short>((void*)free->Data, capacity);
            if (Muted) buffer[..length].Clear();
            else samples[..length].CopyTo(buffer);

            if (!paused && queued > TARGET_QUEUED + 1 && length > 1)
                length--;                                   // the sound card is slower: one sample less
            else if (!paused && queued < TARGET_QUEUED - 1)
            {
                buffer[length] = buffer[length - 1];        // the sound card is faster: one sample repeated
                length++;
            }

            if ((free->Flags & WHDR_PREPARED) != 0) waveOutUnprepareHeader(device, free, (uint)sizeof(WaveHeader));
            free->BufferLength = (uint)(length * sizeof(short));
            free->Flags = 0;
            waveOutPrepareHeader(device, free, (uint)sizeof(WaveHeader));
            waveOutWrite(device, free, (uint)sizeof(WaveHeader));

            if (paused && queued + 1 >= TARGET_QUEUED)
            {
                waveOutRestart(device);
                paused = false;
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;

            waveOutReset(device);   // returns all the buffers at once
            foreach (var header in headers)
            {
                if ((header->Flags & WHDR_PREPARED) != 0) waveOutUnprepareHeader(device, header, (uint)sizeof(WaveHeader));
                NativeMemory.Free((void*)header->Data);
                NativeMemory.Free(header);
            }
            waveOutClose(device);
        }
    }

    #region winmm

    const uint WAVE_MAPPER = 0xFFFFFFFF;
    const uint CALLBACK_NULL = 0;
    const ushort WAVE_FORMAT_PCM = 1;
    const uint WHDR_PREPARED = 0x02;
    const uint WHDR_INQUEUE = 0x10;

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    struct WaveFormat
    {
        public ushort FormatTag;
        public ushort Channels;
        public uint SamplesPerSec;
        public uint AvgBytesPerSec;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WaveHeader
    {
        public IntPtr Data;
        public uint BufferLength;
        public uint BytesRecorded;
        public IntPtr User;
        public uint Flags;
        public uint Loops;
        public IntPtr Next;
        public IntPtr Reserved;
    }

    [DllImport("winmm.dll")]
    static extern uint waveOutOpen(out IntPtr device, uint deviceId, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);

    [DllImport("winmm.dll")]
    static extern uint waveOutPrepareHeader(IntPtr device, WaveHeader* header, uint size);

    [DllImport("winmm.dll")]
    static extern uint waveOutUnprepareHeader(IntPtr device, WaveHeader* header, uint size);

    [DllImport("winmm.dll")]
    static extern uint waveOutWrite(IntPtr device, WaveHeader* header, uint size);

    [DllImport("winmm.dll")]
    static extern uint waveOutPause(IntPtr device);

    [DllImport("winmm.dll")]
    static extern uint waveOutRestart(IntPtr device);

    [DllImport("winmm.dll")]
    static extern uint waveOutReset(IntPtr device);

    [DllImport("winmm.dll")]
    static extern uint waveOutClose(IntPtr device);

    #endregion
}
