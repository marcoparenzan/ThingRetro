namespace ThingRetroEmu.Video;

/// <summary>
/// The video output of a machine (the monitor socket): at the end of a frame the video source sends its picture to the connected monitor, if there is one.
/// There are two kinds of connection, like the connectors of a real machine: the RGB one, where the picture goes as dots (<see cref="IMonitor"/>), and the composite
/// one, where it goes as the voltage of a wire (<see cref="ICompositeMonitor"/>). A source that is not composite is encoded on the way, as the board does. The
/// connection is made with a <see cref="MonitorCable"/>
/// </summary>
public sealed class VideoPort
{
    volatile IMonitor? monitor;
    volatile ICompositeMonitor? composite;
    RgbToCompositeSource? encoder;
    IVideoSource? encoded;
    VideoStandard? encodedStandard;
    CompositeFrame? field;

    VideoSystem selection;
    VideoSystem? detected;
    public event Action? SystemChanged;
    public VideoSystem SystemSelection
    {
        get => selection;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (selection == value) return;
            selection = value; SystemChanged?.Invoke();
        }
    }
    public VideoSystem? DetectedSystem
    {
        get => detected;
        set
        {
            if (value is VideoSystem.Auto || (value.HasValue && !Enum.IsDefined(value.Value)))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (detected == value) return;
            detected = value; SystemChanged?.Invoke();
        }
    }
    public VideoSystem? EffectiveSystem => selection == VideoSystem.Auto ? detected : selection;
    public VideoStandard ResolveStandard(double framesPerSecond) => Standard ??
        (EffectiveSystem is VideoSystem.Pal ? VideoStandard.Pal :
         EffectiveSystem is VideoSystem.Ntsc ? VideoStandard.Ntsc : VideoStandard.For(framesPerSecond));

    public bool IsConnected => monitor is not null || composite is not null;

    /// <summary>The standard of the composite signal; null (the default) follows the frame rate of the source: NTSC from 55 a second up, PAL below.</summary>
    public VideoStandard? Standard { get; set; }

    public void Plug(IMonitor screen)
    {
        if (IsConnected && !ReferenceEquals(monitor, screen))
            throw new InvalidOperationException("A monitor is already connected to the video port: unplug it before connecting another one");
        monitor = screen;
    }

    public void Plug(ICompositeMonitor screen)
    {
        if (IsConnected && !ReferenceEquals(composite, screen))
            throw new InvalidOperationException("A monitor is already connected to the video port: unplug it before connecting another one");
        composite = screen;
    }

    public void Unplug(IMonitor screen)
    {
        if (ReferenceEquals(monitor, screen)) monitor = null;
    }

    public void Unplug(ICompositeMonitor screen)
    {
        if (ReferenceEquals(composite, screen)) composite = null;
    }

    /// <summary>Called by the emulation thread at the end of every frame.</summary>
    public unsafe void Present(IVideoSource source)
    {
        var tv = composite;
        if (tv is not null)
        {
            PresentComposite(source, tv);
            return;
        }
        var screen = monitor;
        if (screen is null) return;
        var pixels = screen.BeginFrame(source.Mode);
        try
        {
            source.Render(pixels);
        }
        finally
        {
            screen.EndFrame();
        }
    }

    void PresentComposite(IVideoSource source, ICompositeMonitor tv)
    {
        var native = source as ICompositeSource;
        var standard = native?.Standard ?? ResolveStandard(source.Mode.FramesPerSecond);
        if (field is null || !ReferenceEquals(field.Standard, standard)) field = new CompositeFrame(standard);
        if (native is not null) native.RenderComposite(field);
        else
        {
            if (encoder is null || !ReferenceEquals(encoded, source) || !ReferenceEquals(encodedStandard, standard))
            {
                encoder = new RgbToCompositeSource(source, standard);
                encoded = source;
                encodedStandard = standard;
            }
            encoder.RenderComposite(field);
        }
        tv.ShowComposite(field);
    }
}
