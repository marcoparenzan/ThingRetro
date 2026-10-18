namespace ThingRetroEmu.Video;

/// <summary>
/// The producer of pictures: a video chip (or a board that draws its own frames). It tells what the picture is
/// (<see cref="Mode"/>) and writes one into a buffer the monitor provides
/// </summary>
public interface IVideoSource
{
    /// <summary>The picture it makes now. A chip that changes its resolution says so here, and the monitor follows at the next frame.</summary>
    VideoMode Mode { get; }

    /// <summary>Writes a frame of <see cref="Mode"/> into <paramref name="pixels"/>: 32-bit BGRA, contiguous rows.</summary>
    unsafe void Render(byte* pixels);
}

/// <summary>
/// A monitor connected to the video output of a machine through a <see cref="MonitorCable"/>. It receives a picture at the end of
/// every frame, of whatever size the source says: the monitor makes room for it (and may change its own size, its aspect ratio, its
/// window) and gives back a buffer of the right size. BeginFrame and EndFrame are called by the emulation thread: the implementation
/// must be thread-safe, and EndFrame must return at once
/// </summary>
public interface IMonitor
{
    /// <summary>Returns a buffer for a frame of this mode (at least <c>mode.ByteCount</c> bytes, valid until <see cref="EndFrame"/>).</summary>
    unsafe byte* BeginFrame(VideoMode mode);

    /// <summary>The frame has been written: show it.</summary>
    void EndFrame();
}
