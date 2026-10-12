using System.Runtime.InteropServices;
using Fake64.C64;

namespace Fake64.WinForms;

// Le casse del PC: un ISpeaker che suona i campioni con l'API audio di Windows (waveOut, winmm).
// Si collega con un AudioCable (new AudioCable(board.AudioOut, speakers)).
// L'emulazione va al passo con l'orologio del PC, la scheda audio con il proprio: i due orologi differiscono di
// poco e la coda verso la scheda si allunga o si accorcia lentamente. Per tenerla alla latenza voluta un blocco
// perde o ripete un campione (lo 0,2%: non si sente) invece di saltare o ripetere un blocco intero
public sealed unsafe class PcSpeakers : ISpeaker, IDisposable
{
    const int BUFFERS = 16;             // 160 ms di coda al massimo
    const int TARGET_QUEUED = 5;        // blocchi in coda in condizioni normali: ~50 ms di latenza

    readonly object gate = new();
    readonly IntPtr device;
    readonly WaveHeader*[] headers = new WaveHeader*[BUFFERS];
    readonly int capacity;              // campioni per buffer
    bool paused;
    bool disposed;

    public int SampleRate { get; }

    // Senza audio la coda continua a scorrere (in silenzio): riattivandolo non c'è ritardo
    public bool Muted { get; set; }

    // Blocchi persi perché la coda era piena e volte in cui si è svuotata (l'emulazione è rimasta indietro)
    public int Overruns { get; private set; }
    public int Underruns { get; private set; }

    public PcSpeakers(int sampleRate = 44100)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("PcSpeakers usa l'audio di Windows");

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
        if (result != 0) throw new InvalidOperationException($"Impossibile aprire il dispositivo audio (errore waveOut {result})");

        // Blocchi fino a 40 ms: AudioPort ne manda da 10, più il campione aggiunto per recuperare
        capacity = sampleRate / 25;
        for (int i = 0; i < BUFFERS; i++)
        {
            headers[i] = (WaveHeader*)NativeMemory.AllocZeroed((nuint)sizeof(WaveHeader));
            headers[i]->Data = (IntPtr)NativeMemory.AllocZeroed((nuint)(capacity * sizeof(short)));
        }

        // Il dispositivo parte in pausa: comincia a suonare quando la coda è piena quanto basta
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
                // La coda si è svuotata: si riempie di nuovo prima di ripartire, invece di suonare a singhiozzo
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
                length--;                                   // la scheda audio è più lenta: un campione in meno
            else if (!paused && queued < TARGET_QUEUED - 1)
            {
                buffer[length] = buffer[length - 1];        // la scheda audio è più veloce: un campione ripetuto
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

            waveOutReset(device);   // restituisce subito tutti i buffer
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
