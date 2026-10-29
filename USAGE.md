# ThingNES usage

Requires the .NET 10 SDK. Run from the repository root:

```powershell
dotnet build src/ThingNES.slnx
dotnet test src/ThingNES.slnx
dotnet run --project src/ThingNES.Cli/ThingNES.Cli -- --help
dotnet run --project src/ThingNES.Cli/ThingNES.Cli -- path/to/test.nes 100000 frame.ppm
dotnet run --project src/ThingNES.Cli/ThingNES.Cli -- path/to/test.nes 1789773 - audio.wav
```

The optional count is a positive number of CPU cycles (default 29781). The runner prints PRG size, elapsed cycles and PPU position, then exits. An optional third argument writes the live 256x240 framebuffer as a P6 PPM image (open with a PPM-capable viewer). If execution stops mid-frame, the buffer contains a mixture of the current and previous frame. The first power-on frame starts without a primed background pipeline; allow at least one pre-render line before judging the image (for example run 100000 cycles). An optional fourth argument streams mono PCM16 audio to WAV at 44.1 kHz; use `-` instead of the image path to export audio only. It has no interactive frontend or real-time playback. Use the separate diagnostic mode below for ROMs implementing the Blargg RAM protocol. Unsupported mappers and NES 2.0 files fail explicitly. Use legally obtained NTSC iNES mapper-0/2/3 images; no ROM downloads are needed for the synthetic unit tests.

Library users construct `NesMachine(Cartridge.Load(bytes))`, set `Controller1.Pressed` using `Buttons`, and advance with `Tick()` or `RunCycles(count)`. Read `Ppu.Framebuffer` for live NES palette indices, or call `FrameImage.WritePpm(stream, machine.Ppu.Framebuffer.Span)` for RGB export. Subscribe to `Apu.SampleProduced` to receive synchronous PCM16 samples on the emulation thread. `WaveWriter` streams samples to a writable, seekable stream and finalizes its header on disposal, leaving the stream open. Create a new machine to power-cycle it.

## Windows desktop

Requires Windows and the .NET 10 Windows Desktop runtime/SDK:

```powershell
dotnet run --project src/ThingNES.Desktop/ThingNES.Desktop
dotnet run --project src/ThingNES.Desktop/ThingNES.Desktop -- path/to/test.nes
```

Open ROM loads a local iNES mapper-0/2/3 image and primes the video pipelines. Unsupported cartridges show an error without replacing the current machine. Video preserves aspect ratio with integer nearest-neighbor scaling when space permits.

Controller 1: **Z = A**, **X = B**, **Shift = Select**, **Enter = Start**, **arrow keys = D-pad**. Input is cleared when the window loses focus. **Ctrl+O** opens a ROM, **F5** toggles pause, **F10** pauses and advances one frame, **F2** power-cycles the machine. Power-cycle recreates the cartridge and clears volatile PRG RAM; this is not a hardware reset or battery-save implementation.

Record WAV starts/stops streaming PCM16 recording using emulated time. Pausing stops sample generation; frame-stepping still records. ROM replacement, power-cycle or closing the window finalizes recording. Click **Enable audio** for opt-in speaker playback on the default Windows audio device; **Mute audio** releases it. Pausing clears queued live audio, and paused frame-stepping is silent while still recording WAV. A playback-only DC/low-pass filter at half gain is applied; WAV export remains unfiltered. Device initialization errors are shown without preventing emulation. The UI uses elapsed-time NTSC pacing and at most three catch-up frames per timer poll, dropping excess backlog under load; it is not audio-clock synchronized. Controller 2 is currently library-only.

Automated tests cover core frame stepping, palette conversion, audio serialization, scheduler credit/catch-up and playback filter behavior, not actual window/key or audio-device interactions. Desktop visual/input behavior and speaker playback require manual verification. Audio uses a bounded queue; starvation produces silence and overflow drops samples instead of growing memory. No game compatibility certification is claimed.

## Diagnostic ROMs

```powershell
dotnet run --project src/ThingNES.Cli/ThingNES.Cli -- --diagnostic path/to/01-basics.nes 30000000
dotnet run --project src/ThingNES.Cli/ThingNES.Cli -- --diagnostic https://raw.githubusercontent.com/christopherpow/nes-test-roms/master/instr_test-v5/rom_singles/01-basics.nes
```

Budget defaults to 30 million CPU cycles. HTTPS URLs are fetched only when explicitly supplied, kept in memory, limited to 1 MiB, and printed with a SHA256 fingerprint. Ordinary runs and unit tests require no network. Ctrl+C cancels. Only legally usable iNES mapper-0/2/3 test images are supported; mapper rejection is not a diagnostic failure.

The runner requires the `$DE $B0 $61` signature at `$6001-$6003`, interprets `$6000`, and reads zero-terminated text at `$6004` without changing CPU open bus. Exit codes: 0 passed; 1 input/download error; 2 ROM reports failure; 3 cycle timeout; 4 reset requested; 5 cancelled/download timeout. Reset requests are reported rather than simulated because board reset timing is not implemented. No signature means timeout, never a false pass. Output is bounded to cartridge RAM and terminal control bytes are sanitized.

### External validation evidence

Source: [Blargg instr_test-v5 mirror](https://github.com/christopherpow/nes-test-roms/tree/master/instr_test-v5). All four runs below returned result code 0 after the Ricoh LXA correction. URLs use a moving branch; fingerprints identify the actual tested bytes. These are instruction-behavior checks, not full PPU/APU/timing or game-compatibility certification.

| ROM | CPU cycles to result | SHA256 |
| --- | ---: | --- |
| 01-basics.nes | 537364 | `4DD1CDD406BC3F747972E7DA314CE8CA89321EB7A836C1CED569EE54AE44A384` |
| 02-implied.nes | 2886549 | `1C4D4FA130CF6FEEBC072543A5CD3627AE71063B56B08642BF43E9A6C6F44996` |
| 03-immediate.nes | 2588740 | `6F7AD8FF31C762C37DEAEE0F323DF03EB94025CF1F3B0343EBE6FE567DA0E943` |
| 04-zero_page.nes | 3452383 | `7A8FEADA4BB4460250C8F05401E5D728878BBE71956756D0B11D488E57EB12FD` |

`03-immediate` initially failed on unofficial opcode `$AB` (ATX/LXA). Ricoh now uses a deterministic `$FF` LXA magic model matching this test; the MOS6502 default remains `$EE`. Unstable instructions can vary on real silicon. No external ROM binaries are bundled.

### Audio validation

`apu_test/rom_singles/1-len_ctr.nes` from the same mirror passed with code 0 after 596921 CPU cycles. SHA256: `AACF86C1D773BADD11392E54506A43A06A3DD0B67A4C255909D1DAF770A4A1E2`. `instr_test-v5/03-immediate.nes` was rerun after APU integration and still passed after 2588740 cycles. Additional DMC validation is listed below; full APU/DMA timing coverage is not claimed.

Audio export now includes DMC sample playback and direct DAC output. It remains a functional approximation: analog filtering, band-limited resampling, held triangle DAC output and exact $4017 delays/IRQ races are not modeled. WAV files may contain DC offset and aliasing and are not reference-quality recordings.

DMC validation from `apu_test/rom_singles/` returned code 0:

| ROM | CPU cycles | SHA256 |
| --- | ---: | --- |
| 7-dmc_basics.nes | 716047 | `547324867EE0BA2AA11401001D8D1288530AA4E0ECAAAC1667CE79980A388EC1` |
| 8-dmc_rates.nes | 835168 | `5D9A79A505B37FA277CACC95A362F7E2A56E59ACE7A698213D78432CC06A8867` |

The rates test initially failed at rate 13; its period was corrected from 85 to 84 CPU cycles and both ROMs were rerun successfully. DMC DMA still uses a simplified fixed four-cycle fetch and pauses OAM DMA. Passing these ROMs does not validate exact DMA arbitration or controller read side effects.

### PPU pipeline validation

Nine new synthetic cases cover packed scroll registers, coarse-X wrap, dot-257 transfer, vertical row-29/31 wrapping, pre-render transfer, PPUDATA axis increments, pattern bit ordering and prefetched CHR data. Existing sprite/background tests now wait for a pre-render line before checking pixels.

The external `ppu_vbl_nmi/rom_singles/01-vbl_basics.nes` returned code 0 after 4230166 CPU cycles, SHA256 `06AEA5AF4EDAB4E3141C939CD5AC9936F8758203B25DCAF84AE1A09DB49E024A`. This validates basic vblank behavior, not the complete background pipeline or NMI races. `instr_test-v5/03-immediate.nes` was rerun successfully (2588740 cycles). Background fetch timing has synthetic coverage; broad external PPU compatibility remains unvalidated.

### Sprite pipeline

Seven added synthetic tests verify secondary OAM copy cadence, next-line activation, primary OAM priority, separate low/high pattern fetches, no wrap of Y=255 onto row zero, and that post-fetch CHR/post-evaluation OAM changes do not alter latched data. Sprites now use the previous line's selection and fetches rather than live CHR/OAM reads at every pixel.

The overflow scan remains simplified and does not reproduce the hardware diagonal n/m bug. OAMADDR/corruption and rendering-toggle races remain unmodeled. These tests are synthetic pipeline checks, not external sprite-ROM certification.

## Cartridge boards

| Mapper | Supported standard layout |
| --- | --- |
| 0 / NROM | 16 or 32 KiB fixed PRG, 8 KiB CHR ROM or RAM |
| 2 / UxROM | 32/64/128/256 KiB PRG, selected 16 KiB low bank + fixed last high bank, 8 KiB CHR RAM |
| 3 / CNROM | 16/32 KiB fixed PRG, 8/16/32 KiB banked CHR ROM |

Horizontal/vertical mirroring is header-defined. Four-screen, NES 2.0 and other layouts are rejected. Extra board-specific RAM/open-bus behavior and CNROM copy-protection variants are not modeled; the existing 8 KiB cartridge RAM remains available.

Bus conflicts are enabled by default in CLI and desktop. For boards known not to have conflicts, use the CLI override:

```powershell
dotnet run --project src/ThingNES.Cli/ThingNES.Cli -- path/to/uxrom.nes 100000 frame.ppm --no-bus-conflicts
```

The override also works in diagnostic mode. Library users can pass `Cartridge.Load(bytes, busConflicts: false)`. The desktop currently always uses the default conflict model. Bank selection resets on a new cartridge/power-cycle.

Fifteen new synthetic mapper cases cover fixed/switched banks, masks, bus conflicts, CHR ROM/RAM, trainer alignment, invalid layouts, PPU wiring and an actual 6502 program jumping into a newly selected UxROM bank. External `03-immediate` NROM regression still passes; UxROM/CNROM games or external mapper-specific ROMs have not yet been validated.






