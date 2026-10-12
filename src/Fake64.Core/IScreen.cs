namespace Fake64;

// Monitor collegato all'uscita video della Board tramite un MonitorCable.
// Ogni frame è un'immagine di MOS6569.SCREEN_WIDTH x SCREEN_HEIGHT pixel a 32 bit (BGRA), righe contigue.
// BeginRender ed EndRender vengono chiamati dal thread di emulazione: l'implementazione deve essere thread-safe
public interface IScreen
{
    unsafe byte* BeginRender();
    unsafe void EndRender();
}
