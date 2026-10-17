# Reference documentation

What Thing65 is implemented from. The files are not in the repository (they belong to their authors): run
`tools/Download-Reference.ps1` to fetch them into the folders below. Everything is public.

## `c65/`: the Commodore 65

| File | What it is | Used for |
| --- | --- | --- |
| `c65manualupdated.txt` | The C65 System Specification (MEGA65 team's corrected text): memory map, 4510, VIC-III, F011, F018, kernel jump table, BASIC 10 | The reference for everything: opcodes and cycles (`MOS4510`), the memory map (`BoardC65.Memory`), registers of the VIC-III, DMAgic list format |
| `C65_MPU_4510_rev1-D390490.pdf` | CSG 4510 data sheet (65CE02 core, mapper, on-chip ports) | `MOS4510`, the `MAP`/`EOM` mapper |
| `C65_VIC3_4567_revB-390489.pdf`, `..._revA-D390489.pdf` | CSG 4567 VIC-III specification | `CSG4567`: modes, bitplanes, DAT, palette, attributes |
| `C65_FDC_revC_GA4165_F011-D390491.pdf` | F011 disk controller | `F011`: registers, commands, buffer pointers |
| `C65_DMAgic_4151_F018-390957.pdf` | F018 DMAgic | `F018`: list formats A and B, modes |
| `C65_memory_controller_revB-252535.pdf` | The memory controller (what decides ROM/RAM/I/O) | `BoardC65.Memory` |
| `C65_ELMER_Pal_Spec-390548.pdf`, `C65_IGOR1_Pal_Spec-390989.pdf` | The PALs of the board | Address decoding |
| `4502-pla-lowres.zip`, `4567-lowres-pinout.zip` | PLA and pinout drawings (unzipped next to them) | Pinouts in `Pinouts.cs` |
| `C64DX-System-Spec-Update-1-May-1-1991.pdf`, `C64D_preliminary_product_definition.pdf` | Earlier / later revisions of the specification | Differences between ROM versions |
| `c65faq20.asc.txt`, `c65cmds.pet.txt` | FAQ and BASIC 10 command list | Behaviour of the ROM, test programs |

## `cpu/`: the 6502 family

| File | What it is |
| --- | --- |
| `cpu_65ce02.txt`, `65ce02.txt` | The 65CE02: opcode table, addressing modes (c64ref and zimmers.net) |
| `cpu_6502.txt`, `cpu_65c02.txt`, `cpu_r65c02.txt` | NMOS 6502, 65C02, Rockwell 65C02: opcode tables, flags, cycles |
| `64doc.txt` | "6502 Instruction Timing" (John West, Marko Mäkelä): cycle by cycle, with the bus accesses |
| `6502-NMOS.extra.opcodes.txt`, `oxyron-opcodes-*.html` | Undocumented opcodes and the opcode matrices |
| `Klaus2m5-*.a65*` | Sources of Klaus Dormann's functional tests (the binaries are fetched by `Download-CpuTests.ps1`) |
| `SingleStepTests-65x02-README.md` | The format of Tom Harte's cycle-accurate tests |

## `c64/`: the C64 chips (C64 mode and the chips the C65 shares)

`VIC-Article.txt` (Christian Bauer, *The MOS 6567/6569 video controller*), `6567_vicII_preliminary.pdf`, `6526.zip` and
`6526cia.zip` (CIA), `6581.zip` (SID), `oxyron-registers-sid.html`, `6510.zip`, `8520_datasheet.pdf` (the CIA of the Amiga,
same core), `c64-pla.txt` and `c64_pla_dissected_r1.1_a4ds.pdf` (the PLA).

## `disk/`

`D81.TXT`: the 1581 / `.d81` format (header, BAM, directory, sector chains), used by `D81Image` and `D81Writer`.

## `mega65/`: the superset

`iomap.txt` (the MEGA65 register map, which contains the C65's), `test-c65mode.md`, `sdcard-and-floppy-drive.md`
(how the F011 is emulated there), `viciv-modes.md`, `open-roms-STATUS.md`, and `mega65-rom-faq.txt`: where a legal C65 ROM comes from
(the C64 Forever package of Cloanto, personal use). The ROM itself is not here and not in the repository.

## `xemu-gpl/`: another emulator, to read

Sources of Xemu's C65 core (Gábor Lénárt, **GPL-2.0**): the memory map (`commodore_65.c`), the DMAgic (`dma65.c`), the VIC-III
(`vic3.c`), the F011 (`f011_core.c`), the `.d81` access (`d81access.c`) and a 65CE02 (`cpu65.c`). They were used to
settle what the specifications leave open (for example, that `PHW` pushes the low byte first). Nothing of them is copied
into Thing65: they stay here, outside git, as a second opinion.

## Where each decision came from

The places where the documents disagree or are silent, and what Thing65 does, are in the comments of the code
(`MOS4510.Cycles.cs`, `F018.cs`, `F011.cs`, `CSG4567.cs`). The main ones:

- 65CE02 cycles: the table of the C65 manual (section 2.3.7.5), not the "timing" of c64ref, which its own author marks as not derived.
- `PHW`: low byte first (Xemu, confirmed by the ROM: BASIC saves `TXTPTR` with it and restores it with two `PLA`).
- DMAgic list: 12 bytes (F018B), as the ROM builds it; 11 bytes (F018A) is available.
- Palette ROM of the VIC-III: the 16 colors of the manual (section 2.4).
