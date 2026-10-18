namespace ThingRetroEmu.Video;

/// <summary>
/// What a picture is: its size in dots, how often a new one comes and how wide its dots are compared with their height. A video chip
/// declares it, a monitor adapts to it, so that the same monitor can show the 403 x 284 of a VIC-II, the 806 x 568 of a VIC-III, the 160 x 192 of an
/// Atari 2600 or the 280 x 192 of an Apple II. The pixels are 32-bit BGRA, rows one after the other with no padding
/// </summary>
/// <param name="Width">Dots across.</param>
/// <param name="Height">Lines.</param>
/// <param name="FramesPerSecond">50 for a PAL machine, 59.94 for an NTSC one.</param>
/// <param name="PixelAspect">Width of a dot over its height as it is on a screen: 1 for square dots, 2 for the 2600's wide ones.</param>
public readonly record struct VideoMode(int Width, int Height, double FramesPerSecond = 50, double PixelAspect = 1)
{
    public int PixelCount => Width * Height;

    /// <summary>The bytes of a frame (4 per dot).</summary>
    public int ByteCount => PixelCount * 4;

    /// <summary>Width over height of the picture as the person sees it (the size in dots times the aspect of the dots).</summary>
    public double DisplayAspect => Width * PixelAspect / Height;
}
