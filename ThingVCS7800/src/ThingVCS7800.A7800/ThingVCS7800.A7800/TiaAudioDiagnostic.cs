using ThingRetroEmu.Audio;
using ThingRetroEmu.Video;
using ThingVCS;
namespace ThingVCS7800.A7800;

public sealed record TiaAudioResult(string Kind, VideoSystem System, long MasterClocks, long AudioClocks, long Samples, bool RegistersProgrammed, bool Passed);

/// <summary>Runs real 6502 writes through the board into the common audio cable. Not hardware validation.</summary>
public sealed class TiaAudioDiagnostic
{
    public TiaAudioResult Run(Stream output, VideoSystem system = VideoSystem.Ntsc, int milliseconds = 1000)
    {
        if (milliseconds is < 20 or > 10000) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        _ = MariaTiming.For(system);
        var host = new DevelopmentComputer(); host.Board.Video.SystemSelection = system;
        host.Insert(DevelopmentRoms.TiaSound());
        using var recorder = new WaveRecorder(output, leaveOpen: true);
        using var cable = new AudioCable(host.Board.Audio, recorder);
        long target = (long)host.Board.Timing.MasterClockHz * milliseconds / 1000;
        while (host.Board.MasterClocks < target) host.Board.Tick();
        var audio = ((TiaInputs)host.Board.TiaInputSocket.Chip!).Audio;
        bool registers = audio.RegisterValue(0x15) == 4 && audio.RegisterValue(0x17) == 7 && audio.RegisterValue(0x19) == 15 &&
            audio.RegisterValue(0x16) == 8 && audio.RegisterValue(0x18) == 3 && audio.RegisterValue(0x1a) == 6;
        bool passed = registers && recorder.SampleCount > 0 && audio.AudioClocks == host.Board.MasterClocks / TiaAudio.MasterClocksPerAudioClock;
        // The common AudioPort delivers complete 10 ms blocks; its partial tail is not flushed.
        return new("Synthetic CPU TIA tone/noise -> common AudioPort -> PCM16 WAV", system, host.Board.MasterClocks,
            audio.AudioClocks, recorder.SampleCount, registers, passed);
    }
}
