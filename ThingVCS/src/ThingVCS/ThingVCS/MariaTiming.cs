using ThingRetroEmu.Video;
namespace ThingVCS;

/// <summary>Functional non-interlaced 7800 timing; active crop/palette are not calibrated.</summary>
public sealed record MariaTiming(VideoSystem System, int MasterClockHz, int Lines, int ActiveLines)
{
    public static MariaTiming Ntsc { get; } = new(VideoSystem.Ntsc, 7159092, 262, 242);
    public static MariaTiming Pal { get; } = new(VideoSystem.Pal, 7093788, 312, 292);
    public double FramesPerSecond => (double)MasterClockHz / (Maria.DotsPerLine * Lines);
    public static MariaTiming For(VideoSystem system) => system switch
    {
        VideoSystem.Pal => Pal,
        VideoSystem.Ntsc => Ntsc,
        _ => throw new ArgumentOutOfRangeException(nameof(system))
    };
}
