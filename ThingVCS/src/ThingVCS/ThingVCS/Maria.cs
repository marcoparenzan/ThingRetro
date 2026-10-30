using ThingRetroEmu.Video;
namespace ThingVCS;

/// <summary>Logical MARIA register/clock/video and WSYNC output pins; DMA pins will be added with DMA.</summary>
public interface IMariaPinout : IVideoSource
{
    ThingRetroEmu.Chips.IBus? Bus { get; set; }
    Func<bool>? BusBoundaryInput { get; set; }
    bool WaitSync { get; }
    bool DmaBusy { get; }
    bool Nmi { get; } // Logical asserted /NMI output from DLI.
    long Frames { get; }
    byte ReadRegister(int register, byte openBus);
    void WriteRegister(int register, byte value);
    void Tick(); // One master-clock step, not a CPU cycle.
    void Reset();
}

/// <summary>Functional PAL/NTSC MARIA raster with buffered line RAM and master-clock DMA sequencing.</summary>
public sealed partial class Maria : IMariaPinout
{
    public const int DotsPerLine = 454, LinesPerFrame = 262;
    public const int FirstVisibleLine = 16, VisibleLines = 242, Width = 320;
    readonly byte[] registers = new byte[32];
    byte[] pixels = new byte[Width * VisibleLines];
    public MariaTiming Timing { get; private set; } = MariaTiming.Ntsc;
    public void Configure(VideoSystem system)
    {
        var timing = MariaTiming.For(system);
        if (Timing == timing) return;
        Timing = timing;
        pixels = new byte[Width * Timing.ActiveLines];
        Reset();
    }
    public ReadOnlyMemory<byte> Framebuffer => pixels;
    public int Dot { get; private set; }
    public int Scanline { get; private set; }
    public long Frames { get; private set; }
    public bool WaitSync { get; private set; }
    public bool Vblank => Scanline < FirstVisibleLine || Scanline >= FirstVisibleLine + Timing.ActiveLines;
    public ushort DisplayListPointer => (ushort)((registers[12] << 8) | registers[16]);
    public byte Control => registers[28];
    public VideoMode Mode => new(Width, Timing.ActiveLines, Timing.FramesPerSecond);
    public byte ReadRegister(int register, byte openBus) => (register & 31) == 8
        ? (byte)((openBus & 0x7f) | (Vblank ? 0x80 : 0)) : openBus;
    public void WriteRegister(int register, byte value)
    {
        int index = register & 31;
        if (index == 4) WaitSync = true;
        else if (index != 8) registers[index] = value;
    }
    public void Tick()
    {
        if (nmiClocks > 0) nmiClocks--;
        StepDma();
        if (!Vblank && Dot == 0)
        {
            // Display the previous DMA result, prepare the other bank for the next line.
            writeBuffer ^= 1;
            BeginLine();
        }
        if (Scanline == FirstVisibleLine - 1 && Dot == 0)
        {
            // Do not carry stale line RAM across vertical blank.
            foreach (var buffer in lineBuffers) Array.Clear(buffer);
        }
        // Provisional active window; palette is sampled at output time, graphics at line preparation.
        if (!Vblank && Dot is >= 68 and < 388)
        {
            int cell = outputLineRam[(Dot - 68) / 2];
            int paletteIndex = DecodePaletteIndex(cell, Control & 3, ((Dot - 68) & 1) != 0);
            pixels[(Scanline - FirstVisibleLine) * Width + Dot - 68] = (paletteIndex & 3) == 0 ? registers[0] : registers[paletteIndex];
        }
        MasterClocks++;
        if (++Dot < DotsPerLine) return;
        Dot = 0; WaitSync = false;
        if (++Scanline < Timing.Lines) return;
        Scanline = 0; Frames++;
    }
    static int DecodePaletteIndex(int cell, int readMode, bool second) => readMode switch
    {
        2 => (cell & 0x10) | (second
            ? ((cell << 1) & 2) | ((cell >> 2) & 1)
            : (cell & 2) | ((cell >> 3) & 1)),
        3 => (cell & 0x1c) | (second ? (cell << 1) & 2 : cell & 2),
        _ => cell
    };
    public void Reset()
    {
        Array.Clear(registers); Array.Clear(pixels); ResetDisplayLists();
        Dot = Scanline = 0; Frames = 0; WaitSync = false;
    }
    // Approximate hue/luminance visualization, NOT a measured Atari NTSC palette or composite encoder.
    public static int ApproximateArgb(byte color, bool monochrome = false)
    {
        double y = (color & 15) / 15.0;
        int hue = color >> 4;
        double angle = (hue - 1) * 2 * Math.PI / 15;
        double i = hue == 0 || monochrome ? 0 : .22 * Math.Cos(angle);
        double q = hue == 0 || monochrome ? 0 : .22 * Math.Sin(angle);
        int r = (int)Math.Clamp((y + .956 * i + .621 * q) * 255, 0, 255);
        int g = (int)Math.Clamp((y - .272 * i - .647 * q) * 255, 0, 255);
        int b = (int)Math.Clamp((y - 1.106 * i + 1.703 * q) * 255, 0, 255);
        return unchecked((int)0xff000000) | (r << 16) | (g << 8) | b;
    }
    public unsafe void Render(byte* destination)
    {
        var target = new Span<int>(destination, pixels.Length);
        for (int index = 0; index < pixels.Length; index++)
            target[index] = ApproximateArgb(pixels[index], (Control & 0x80) != 0);
    }
}
