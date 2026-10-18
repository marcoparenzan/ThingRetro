# Decisions

| Date | Decision | Why |
| --- | --- | --- |
| 2026-10-12 | Thing64 (then Fake64) is written entirely in English | The user wants the whole project in English; chat stays in Italian |
| 2026-10-13 | Fake64 renamed Thing64 | |
| 2026-10-17 | Thing65 (C65): command line and WinForms only; the web is deferred | Web came later (2026-10-18: it runs the C65) |
| 2026-10-18 | The drives are a separate project, Thing1500, connected by an explicit IEC cable | A computer and a drive must not know each other |
| 2026-10-18 | ROMs are not versioned; they are removed from the history of Thing64 and Thing65 | Copyright. `tools/Install-Roms.ps1` installs them from C64 Forever Free Express Edition |
| 2026-10-18 | The shared chips go to one package, **ThingRetroEmu** (chips, sockets, buses, media, assembler), not several | Simpler; separable later by folder. Replaces the earlier idea of ThingMOS and ThingRetroChips |
| 2026-10-18 | The `SO` pin of the 6502 is a real feature, not a workaround | The 1541 signals "byte ready" to its CPU with it |
| 2026-10-18 | Cross-project log is **ThingCommodore** (this repository), documents only | Each repo keeps its own files |
| 2026-10-18 | Thing65 and Thing1500 repositories are private for now | Can be made public later, not the other way round |
| 2026-10-18 | No `Co-Authored-By` in commits | User preference |
| 2026-10-18 | The drives move from Thing1500 to **ThingCommodore**, which becomes the repository of the peripherals (1541, 1581, C2N, MPS-803) and keeps the log of the family in `docs/log`. Thing1500 is deleted later | One repository for the machines that plug into the computers |
| 2026-10-18 | Thing64 uses ThingRetroEmu: `Thing64.Mos` and `Thing64.Asm` are removed | Single source of the chips; 556 tests of Thing64 pass on the library |
| 2026-10-18 | The cassette recorder records the rising edges of the WRITE line | Every pulse the KERNAL writes starts with one; measuring the falling edges gave pulses of the wrong length |
| 2026-10-18 | The 1581 is made from what its ROM does (the service manual is a scan): the DOS tells the bits of the 8520 ports and the WD1770 behaviour it needs | Verified by LOAD, SAVE and format with the real DOS |
| 2026-10-18 | The printer is a listener at the level of the IEC protocol, with no CPU or ROM | The MPS-803 firmware is not needed to print; the font is the computer's character generator |
