namespace Fake64;

public interface IScreen
{
    unsafe byte* BeginRender();
    unsafe void EndRender();
}
