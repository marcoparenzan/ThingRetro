namespace Fake64.C64;

// Monitor connected to the Board's video output through a MonitorCable.
// Each frame is a picture of MOS6569.SCREEN_WIDTH x SCREEN_HEIGHT 32-bit pixels (BGRA), contiguous rows.
// BeginRender and EndRender are called by the emulation thread: the implementation must be thread-safe
public interface IScreen
{
    unsafe byte* BeginRender();
    unsafe void EndRender();
}
