# DLI timing matrix — synthetic, not hardware certification

```powershell
dotnet run --project src/CartridgeInspector/CartridgeInspector -- --dli-matrix ops/references/dli-matrix
```

Exports four generated raw ROMs and dli-matrix.json. JSON records each DLI master clock, scanline, dot and HALT. Handler counts accumulate byte-counter deltas across board ticks so wrapping cannot conceal missing interrupts.

## Current two-frame results

| System | Startup | Generated | Serviced | Unserviced | Minimum spacing |
|---|---|---:|---:|---:|---:|
| NTSC | off | 480 | 480 | 0 | 452 |
| NTSC | on | 482 | 482 | 0 | 452 |
| PAL | off | 580 | 580 | 0 | 452 |
| PAL | on | 582 | 582 | 0 | 452 |

All cases pass and the command exits 0. Bootstrap initializes/renders the first zone without decrementing its offset or acquiring the successor. The countdown starts the following line, consistent with the frame_scanline > 16 guard in archived MAME startdma. Combined with the existing final-active-line successor guard, this yields ActiveLines-2 successor events plus optional startup per frame. The first offset is held in the line RAM pipeline, explicitly covered by tests.

Before this correction, startup and successor were only 23 clocks apart within one HALT; two requests were unserviced across two frames. No artificial NMI queue or CPU latch change was made. The generated fixture's DLL/list/graphics are separated for 242/292-line profiles. PAL remains provisional. Eight-clock NMI pulse width, final active crop and exact hardware phases require independent validation. No externally authored timing ROM or hardware capture was run.

Summer Games and Winter Games were rerun for 180 frames with their existing RAM corrections; each reported zero graphic truncations and zero DLI clamps. This does not establish full gameplay or visual compatibility.
