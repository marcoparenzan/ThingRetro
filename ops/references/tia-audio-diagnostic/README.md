# Synthetic TIA audio diagnostic

```powershell
dotnet run --project src/CartridgeInspector/CartridgeInspector -- --tia-audio-diagnostic ops/references/tia-audio-diagnostic
```

Outputs: generated raw 16 KiB ROM `tia-tone-noise.bin`, `tia-Ntsc.wav`, `tia-Pal.wav` and `result.json`. WAVs contain one emulated second, PCM16 mono at 44100 Hz, passed through the common AudioPort resampling/DC-removal stage. The ROM programs channel 0 pure tone (AUDC=4, AUDF=7, AUDV=15), channel 1 polynomial noise (8,3,6), backdrop colour, then loops. Writes are executed by SALLY, not injected directly into chip registers.

Measured: NTSC 31399 audio clocks; PAL 31113. Both files contain 44100 samples, and CPU-programmed registers match expected values. Tests validate nonzero PCM/header lengths and the common recorder's sample endianness, finalization, empty recordings, lifetime and invalid inputs. The recorder is ThingRetroEmu.Audio.WaveRecorder implementing ISpeaker: it can be attached using the common AudioCable to other machines too. The Atari frontend offers the generated ROM through its existing Cartridge menu; enable audio separately.

Synthetic integration validation only: no physical recording or external diagnostic was compared, and Windows listening has not been tested. Polynomial phases/gating remain functional approximations. Header finalization occurs on recorder disposal; complete 10 ms AudioPort blocks are captured, but its pending sub-block tail is not flushed for arbitrary durations. Files are overwritten by rerunning the command.
