namespace Fake64.C64;

// Altoparlante (casse, amplificatore, registratore...) collegato all'uscita audio della Board tramite un AudioCable.
// Riceve campioni mono a 16 bit con segno, alla frequenza che dichiara. Play viene chiamato dal thread
// di emulazione ogni 10 ms di audio emulato: l'implementazione deve copiare i campioni e tornare subito
public interface ISpeaker
{
    int SampleRate { get; }
    void Play(ReadOnlySpan<short> samples);
}
