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
