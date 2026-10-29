# ThingNES roadmap

## Implemented foundation
- .NET 10 headless NTSC runner and xUnit hardware tests.
- Existing MOS6502 cycle engine reused through a Ricoh CPU derivative; decimal flag retained, decimal arithmetic disabled.
- iNES mapper 0: 16/32 KiB PRG, CHR ROM/RAM, trainer loading, nametable mirroring.
- UxROM (mapper 2) with 32-256 KiB PRG and 8 KiB CHR RAM; CNROM (mapper 3) with 16/32 KiB PRG and 8/16/32 KiB CHR ROM. Bank selection and optional bus conflicts.
- CPU RAM/register mirroring, two serial controllers, cycle-stepped OAM DMA.
- PPU memory/register access, buffered reads, palette aliases, vblank/NMI and 3:1 clocks.
- Functional 256x240 background/sprite rendering, attributes, scrolling across nametables, clipping, grayscale, sprite priority/flips/8x16, sprite-zero hit and simple eight-sprite overflow.
- Rendering-dependent odd-frame dot skip and optional PPM frame export.
- Functional NTSC APU: two pulse channels with sweep/envelopes, triangle linear counter, noise LFSR, length counters and frame IRQ; 44.1 kHz PCM sample output and streaming WAV export.
- DMC registers, sample buffering/bit playback, 7-bit DAC, NTSC rates, loop, IRQ, address wrap and fixed four-cycle DMA fetches.
- Background fetch/shift pipeline with v/t/x/w registers, fine X, coarse X/Y wraps, dot-257 horizontal transfer, pre-render vertical transfers and rendering-time PPUDATA increments.
- Secondary OAM clearing/copying over dots 1-256 and sprite pattern fetches over 257-320, with next-line latched rows and retained primary OAM identity for sprite-zero hit.
- Windows WinForms frontend with ROM opening, nearest-neighbor video, keyboard controller 1, pause/frame-step, power-cycle and streamed WAV recording.
- Opt-in NAudio/WinMM live PCM output with a bounded queue and playback-only DC/low-pass filtering.
- Stopwatch-based NTSC frame pacing with bounded catch-up and pause/resume reset.
- 94 passing synthetic hardware/rendering/diagnostic/audio/frontend-support/mapper tests; actual UI/audio-device interactions still require manual validation.
- External ppu_vbl_nmi/01-vbl_basics passed; CPU 03-immediate rechecked after the pipeline change.
- External apu_test singles 7-dmc_basics and 8-dmc_rates passed; rate 13 corrected to 84 cycles.
- External apu_test/rom_singles/1-len_ctr.nes passed; instr_test-v5/03-immediate rechecked after APU integration.
- Blargg $6000 protocol runner, HTTPS/local ROM input, bounded execution, cancellation and process exit codes.
- External instr_test-v5 singles 01-basics, 02-implied, 03-immediate and 04-zero_page passed. See USAGE for evidence and limitations.

## Next
- Sprite overflow diagonal n/m bug, OAMADDR/corruption and evaluation edge cases; validate both PPU pipelines against additional ROMs; manual desktop/input validation and improved pacing.
- Register race behavior and accurate DMA/RDY arbitration.
- Exact DMC/OAM DMA and CPU RDY/write arbitration, repeated-read side effects, frame IRQ/write-delay behavior, accurate analog filtering/resampling, audio-device validation and audio-clock synchronization.
- Expand external CPU/PPU/APU diagnostic coverage, implement requested board resets, then validate NROM game compatibility.
- Battery-backed saves, NES 2.0, MMC1/MMC3 and additional board variants; PAL/2A07 later.
- Consider extracting mature Ricoh chips into ThingRicoh once component contracts stabilize.

## Not claimed
This foundation has a Windows interactive frontend, opt-in live audio and WAV export. Stopwatch pacing targets NTSC with bounded catch-up but is not synchronized to the audio device clock. Actual UI and audio-device behavior have not been manually verified. The APU omits analog filters, exact $4017 write delays/IRQ races and held triangle DAC behavior; samples are not band-limited. DMC fetches use a fixed four-cycle stall and pause OAM DMA, not exact hardware arbitration; controller read glitches and DMA startup/disable races are deferred. Game compatibility is not validated. It is not a cycle-perfect NES emulator; background scrolling now uses v/t transfers, but sprite overflow/OAMADDR edge cases and register races remain simplified, and color emphasis is not modeled. CPU reset currently inherits the MOS core's instantaneous vector fetch. No copyrighted ROMs are bundled. A complete studio is out of scope for the first milestone.
