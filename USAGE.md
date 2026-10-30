# ThingRetro usage

## Atari 7800: WebComputer / WebStudio

- WebComputer: `dotnet run --project ThingVCS7800/src/ThingVCS7800.WebComputer/ThingVCS7800.WebComputer --no-launch-profile --urls http://localhost:7800`
- WebStudio: `dotnet run --project ThingVCS7800/src/ThingVCS7800.WebStudio/ThingVCS7800.WebStudio --no-launch-profile --urls http://localhost:7801`

Usare due terminali; aprire i rispettivi URL. Nel Computer aprire Peripherals per caricare la cartuccia o inserire la demo. Il checkbox conferma solo la correzione RAM per impronte note Summer/Winter Games, non altri fallback legacy. Click sul monitor per frecce/Ctrl/Shift. Run avanza a 10 frame/s nominali, non velocità NES/Atari reale; Frame e Reset sono espliciti.

Nello Studio modificare il template 6502, poi Build & load. Download source salva `main.asm`; Download last build .a78 esporta l'ultima build riuscita (può differire dal sorgente modificato successivamente). Sono ammesse solo ROM lineari 16 KiB a $C000; niente file include/import o loop assembler. I progetti sono in memoria del circuito, non nel filesystem server: salvare il sorgente prima di chiudere. Audio browser, debugger avanzato ed editor grafici non implementati. Usare solo in locale.

## Atari 7800: BIOS opzionale

Nel Computer usa **Peripherals → BIOS / boot → Install BIOS (4/16 KiB)…** con un firmware locale di cui disponi legittimamente. L'installazione riavvia la scheda; il vettore reset viene letto dal BIOS. **Remove BIOS / direct cartridge boot** torna all'avvio diretto precedente. Nessun BIOS Atari è incluso o scaricato. Inserisci una cartuccia per avanzare l'esecuzione nel Computer; un BIOS installato senza cartuccia non rende attivo il pacing della finestra.

Il latch di controllo e l'overlay hanno copertura sintetica: non è ancora verificato il boot di un BIOS Atari reale o il controllo delle firme. La modalità 2600 ha solo una base core/API per cartucce lineari 2/4 KiB (`DevelopmentComputer.Insert2600`): non apre automaticamente giochi 2600 nella form e non produce ancora video TIA.

## Atari 7800: joystick a due pulsanti

Il selettore controller comune usa ora entrambi i pulsanti nel Computer 7800:

- Frecce: **Ctrl = fire 1**, **Shift = fire 2**.
- Tastierino: **0/5 = fire 1**, **Decimal = fire 2**.
- USB: pulsanti 1 e 2.

La ROM sceglie la modalità tramite RIOT: PB2 per porta 1, PB4 per porta 2, configurati come uscite basse per due pulsanti. Il reset lascia inizialmente la compatibilità a un pulsante; in quella modalità entrambi i fire confluiscono nell'ingresso fire tradizionale. Non occorre uno switch nella form. La prova tastiera/USB e la compatibilità con giochi reali restano da verificare manualmente. Gli altri Computer mantengono i default dei simulatori comuni.

## Atari 7800: registrazione WAV dal Computer

Avvia `dotnet run --project ThingVCS7800/src/ThingVCS7800.Computer`, inserisci una cartuccia e scegli **Peripherals → Audio recording → Start WAV recording…** (anche nel menu contestuale). Il titolo mostra REC WAV. **Stop and finalize WAV** chiude il file PCM16 mono a 44,1 kHz.

Puoi ascoltare e registrare contemporaneamente; mute disabilita solo l'ascolto. La registrazione funziona anche senza dispositivo audio Windows. Pausa non registra silenzio aggiuntivo. Reset, cambio cartuccia riuscito, espulsione, cambio standard video e chiusura finalizzano automaticamente. Il caricamento rifiutato non interrompe la registrazione. Il registratore cattura l'audio dopo i filtri comuni, non il DAC grezzo; confini iniziali/finali approssimati ai blocchi da 10 ms.

Per una prova senza giochi: **Cartridge → Insert TIA tone/noise test ROM**. Per esportare la diagnostica headless: `dotnet run --project src/CartridgeInspector/CartridgeInspector -- --tia-audio-diagnostic ops/references/tia-audio-diagnostic`. L'interfaccia Windows e l'ascolto restano da verificare manualmente.

## Atari 7800: diagnostica DLI post-render

Dalla radice, con .NET 10:

```powershell
dotnet run --project src/CartridgeInspector/CartridgeInspector -- --dli-diagnostic ops/references/dli-diagnostic
```

Esegue una **diagnostica sintetica NTSC**, restituisce exit code 0 solo se le verifiche passano e salva ROM raw generata e risultato JSON nella cartella indicata. Attesi su due frame: 482 eventi DLI, contatore handler 226 (482 modulo 256), 8194 master clock di overhead e zero clamp/troncamenti.

La ROM è `ops/references/dli-diagnostic/post-render-dli-ntsc.bin`, apribile nel Computer con standard Auto/NTSC. Non è una ROM diagnostica esterna: esclude il DLI iniziale per isolare le transizioni post-render. La durata dell'impulso NMI e il timing iniziale restano provvisori. Il test separato con startup DLI attivo documenta una coalescenza nel modello corrente (242 segnali, 241 handler per frame), non un comportamento hardware certificato.

## ThingNES usage

Requires the .NET 10 SDK. Run from the repository root:

```powershell
dotnet build ThingNES/src/ThingNES.slnx
dotnet test ThingNES/src/ThingNES.slnx
dotnet run --project ThingNES/src/ThingNES.Cli/ThingNES.Cli -- --help
dotnet run --project ThingNES/src/ThingNES.Cli/ThingNES.Cli -- path/to/test.nes 100000 frame.ppm
dotnet run --project ThingNES/src/ThingNES.Cli/ThingNES.Cli -- path/to/test.nes 1789773 - audio.wav
```

The optional count is a positive number of CPU cycles (default 29781). The runner prints PRG size, elapsed cycles and PPU position, then exits. An optional third argument writes the live 256x240 framebuffer as a P6 PPM image (open with a PPM-capable viewer). If execution stops mid-frame, the buffer contains a mixture of the current and previous frame. The first power-on frame starts without a primed background pipeline; allow at least one pre-render line before judging the image (for example run 100000 cycles). An optional fourth argument streams mono PCM16 audio to WAV at 44.1 kHz; use `-` instead of the image path to export audio only. It has no interactive frontend or real-time playback. Use the separate diagnostic mode below for ROMs implementing the Blargg RAM protocol. Unsupported mappers and NES 2.0 files fail explicitly. Use legally obtained NTSC iNES mapper-0/2/3 images; no ROM downloads are needed for the synthetic unit tests.

Library users construct `NesMachine(Cartridge.Load(bytes))`, set `Controller1.Pressed` using `Buttons`, and advance with `Tick()` or `RunCycles(count)`. Read `Ppu.Framebuffer` for live NES palette indices, or call `FrameImage.WritePpm(stream, machine.Ppu.Framebuffer.Span)` for RGB export. Subscribe to `Apu.SampleProduced` to receive synchronous PCM16 samples on the emulation thread. `WaveWriter` streams samples to a writable, seekable stream and finalizes its header on disposal, leaving the stream open. Create a new machine to power-cycle it.

## Windows desktop

Requires Windows and the .NET 10 Windows Desktop runtime/SDK:

```powershell
dotnet run --project ThingNES/src/ThingNES.Computer/ThingNES.Computer
dotnet run --project ThingNES/src/ThingNES.Computer/ThingNES.Computer -- path/to/test.nes
```

**Peripherals → Cartridge → Insert cartridge…** loads a local iNES mapper-0/2/3 image and primes the pipelines; startup arguments and drag/drop remain supported. Unsupported cartridges preserve the current machine. ThingNES.Computer replaces the former Desktop project: the window now uses the same common MonitorControl and VideoMenu as Thing64/7800, with top Peripherals/Video menus and screen context menu, no toolbar/help/status strips.

Controller 1: **Z = A**, **X = B**, **Shift = Select**, **Enter = Start**, **arrow keys = D-pad**. Input is cleared when the window loses focus. **Ctrl+O** opens a ROM, **F5** toggles pause, **F10** pauses and advances one frame, **F2** power-cycles the machine. Power-cycle recreates the cartridge and clears volatile PRG RAM; this is not a hardware reset or battery-save implementation.

**Peripherals → Audio recording** starts/stops a common WaveRecorder, with AudioSplitter allowing simultaneous PC speakers and WAV. Toggle **Audio** in the screen context menu; speaker mute does not mute recording. The core exposes AudioPort at the existing 44.1 kHz APU PCM source rate. Computer recordings now include the common AudioPort filter/gain; CLI WaveWriter raw exports remain unchanged. Pause stops sample generation; frame stepping generates audio if enabled and still records. ROM replacement, reset/power-cycle, eject or close finalizes WAV. Speaker queue draining at pause is not explicitly flushed in this migration. Device initialization errors do not prevent emulation/recording. NTSC FramePacer remains elapsed-time and bounded; no audio-clock synchronization. Controller 2 remains library-only; no DE-9 pin mapping is invented for NES controllers.

Automated tests cover core frame stepping, palette conversion, audio serialization, scheduler credit/catch-up and playback filter behavior, not actual window/key or audio-device interactions. Desktop visual/input behavior and speaker playback require manual verification. Audio uses a bounded queue; starvation produces silence and overflow drops samples instead of growing memory. No game compatibility certification is claimed.

## Diagnostic ROMs

```powershell
dotnet run --project ThingNES/src/ThingNES.Cli/ThingNES.Cli -- --diagnostic path/to/01-basics.nes 30000000
dotnet run --project ThingNES/src/ThingNES.Cli/ThingNES.Cli -- --diagnostic https://raw.githubusercontent.com/christopherpow/nes-test-roms/master/instr_test-v5/rom_singles/01-basics.nes
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
dotnet run --project ThingNES/src/ThingNES.Cli/ThingNES.Cli -- path/to/uxrom.nes 100000 frame.ppm --no-bus-conflicts
```

The override also works in diagnostic mode. Library users can pass `Cartridge.Load(bytes, busConflicts: false)`. The desktop currently always uses the default conflict model. Bank selection resets on a new cartridge/power-cycle.

Fifteen new synthetic mapper cases cover fixed/switched banks, masks, bus conflicts, CHR ROM/RAM, trainer alignment, invalid layouts, PPU wiring and an actual 6502 program jumping into a newly selected UxROM bank. External `03-immediate` NROM regression still passes; UxROM/CNROM games or external mapper-specific ROMs have not yet been validated.






