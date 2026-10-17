<#
.SYNOPSIS
    Downloads the reference documentation Thing65 is implemented from, into docs/reference.

.DESCRIPTION
    The documents belong to their authors and are not part of the repository (docs/reference is excluded from git,
    only its README.md is kept): this script fetches them again on any machine. Everything is public and freely
    downloadable. Files that are already there are skipped; use -Force to download them again.

    - Chip specifications of the C65 (CSG 4510, 4567 VIC-III, F011, F018 DMAgic, memory controller, PALs), the
      C65 System Specification and the 65CE02 description.
    - The 6502 family: opcode tables and cycle counts (6502, 65C02, 65CE02), the 64doc.
    - The C64 chips: VIC-II (Christian Bauer's article), 6526 CIA, 6581 SID, 6510, the PLA.
    - The .d81 disk format.
    - Implementations used as a cross-check (Xemu's C65 sources, GPL-2.0, for reading only: nothing of them is
      used in Thing65), the MEGA65 core documentation, the test suites' descriptions.

.PARAMETER Force
    Downloads again the files that exist.
#>
param([switch]$Force)

$ErrorActionPreference = 'Continue'
$root = Join-Path (Split-Path -Parent $PSScriptRoot) 'docs/reference'
New-Item -ItemType Directory -Force $root | Out-Null

$chipdata = 'https://www.zimmers.net/anonftp/pub/cbm/documents/chipdata'
$manuals = 'https://www.zimmers.net/anonftp/pub/cbm/manuals/c65'
$xemu = 'https://raw.githubusercontent.com/lgblgblgb/xemu/master'
$mega65 = 'https://raw.githubusercontent.com/MEGA65/mega65-core/development'

# folder, file name, URL
$files = @(
    # --- C65 chips (Commodore's own specifications) ---
    @('c65', 'C65_MPU_4510_rev1-D390490.pdf',               "$chipdata/C65_MPU_4510_rev1-D390490.pdf"),
    @('c65', 'C65_VIC3_4567_revB-390489.pdf',               "$chipdata/C65_VIC3_4567_revB-390489.pdf"),
    @('c65', 'C65_VIC3_4567_revA-D390489.pdf',              "$chipdata/C65_VIC3_4567_revA-D390489.pdf"),
    @('c65', 'C65_FDC_revC_GA4165_F011-D390491.pdf',        "$chipdata/C65_FDC_revC_GA4165_F011-D390491.pdf"),
    @('c65', 'C65_DMAgic_4151_F018-390957.pdf',             "$chipdata/C65_DMAgic_4151_F018-390957.pdf"),
    @('c65', 'C65_memory_controller_revB-252535.pdf',       "$chipdata/C65_memory_controller_revB-252535.pdf"),
    @('c65', 'C65_ELMER_Pal_Spec-390548.pdf',               "$chipdata/C65_ELMER_Pal_Spec-390548.pdf"),
    @('c65', 'C65_IGOR1_Pal_Spec-390989.pdf',               "$chipdata/C65_IGOR1_Pal_Spec-390989.pdf"),
    @('c65', '4502-pla-lowres.zip',                         "$chipdata/4502-pla-lowres.zip"),
    @('c65', '4567-lowres-pinout.zip',                      "$chipdata/4567-lowres-pinout.zip"),
    # --- C65 system ---
    @('c65', 'c65manualupdated.txt',                        'https://raw.githubusercontent.com/MEGA65/c65-specifications/master/c65manualupdated.txt'),
    @('c65', 'c65-specifications-README.md',                'https://raw.githubusercontent.com/MEGA65/c65-specifications/master/README.md'),
    @('c65', 'C64DX-System-Spec-Update-1-May-1-1991.pdf',   "$manuals/C64DX-System-Spec-Update-1-May-1-1991.pdf"),
    @('c65', 'C64D_preliminary_product_definition.pdf',     "$manuals/C64D_preliminary_product_definition.pdf"),
    @('c65', 'c65faq20.asc.txt',                            "$manuals/c65faq20.asc.txt"),
    @('c65', 'c65cmds.pet.txt',                             "$manuals/c65cmds.pet.txt"),
    # --- CPU: 6502 family ---
    @('cpu', 'cpu_6502.txt',                                'https://pagetable.com/c64ref/6502/cpu_6502.txt'),
    @('cpu', 'cpu_65c02.txt',                               'https://pagetable.com/c64ref/6502/cpu_65c02.txt'),
    @('cpu', 'cpu_r65c02.txt',                              'https://pagetable.com/c64ref/6502/cpu_r65c02.txt'),
    @('cpu', 'cpu_65ce02.txt',                              'https://pagetable.com/c64ref/6502/cpu_65ce02.txt'),
    @('cpu', '65ce02.txt',                                  "$chipdata/65ce02.txt"),
    @('cpu', '64doc.txt',                                   "$chipdata/64doc"),
    @('cpu', '6502-NMOS.extra.opcodes.txt',                 "$chipdata/6502-NMOS.extra.opcodes"),
    @('cpu', 'oxyron-opcodes-6502.html',                    'http://www.oxyron.de/html/opcodes02.html'),
    @('cpu', 'oxyron-opcodes-65c02.html',                   'https://www.oxyron.de/html/opcodesc02.html'),
    @('cpu', 'Klaus2m5-6502_functional_test.a65',           'https://raw.githubusercontent.com/Klaus2m5/6502_65C02_functional_tests/master/6502_functional_test.a65'),
    @('cpu', 'Klaus2m5-65C02_extended_opcodes_test.a65c',   'https://raw.githubusercontent.com/Klaus2m5/6502_65C02_functional_tests/master/65C02_extended_opcodes_test.a65c'),
    @('cpu', 'SingleStepTests-65x02-README.md',             'https://raw.githubusercontent.com/SingleStepTests/65x02/main/README.md'),
    # --- C64 chips (the compatibility mode of the C65) ---
    @('c64', 'VIC-Article.txt',                             'https://www.cebix.net/VIC-Article.txt'),
    @('c64', '6567_vicII_preliminary.pdf',                  "$chipdata/6567_vicII_preliminary.pdf"),
    @('c64', '6526.zip',                                    "$chipdata/6526.zip"),
    @('c64', '6526cia.zip',                                 "$chipdata/6526cia.zip"),
    @('c64', '6581.zip',                                    "$chipdata/6581.zip"),
    @('c64', '6510.zip',                                    "$chipdata/6510.zip"),
    @('c64', '8520_datasheet.pdf',                          "$chipdata/8520_datasheet.pdf"),
    @('c64', 'c64-pla.txt',                                 "$chipdata/c64-pla.txt"),
    @('c64', 'c64_pla_dissected_r1.1_a4ds.pdf',             "$chipdata/c64_pla_dissected_r1.1_a4ds.pdf"),
    @('c64', 'oxyron-registers-sid.html',                   'http://www.oxyron.de/html/registers_sid.html'),
    # --- Disk format ---
    @('disk', 'D81.TXT',                                    'https://ist.uwaterloo.ca/~schepers/formats/D81.TXT'),
    # --- MEGA65 (a superset of the C65: documentation of its own implementation) ---
    @('mega65', 'iomap.txt',                                "$mega65/iomap.txt"),
    @('mega65', 'test-c65mode.md',                          "$mega65/docs/test-c65mode.md"),
    @('mega65', 'sdcard-and-floppy-drive.md',               "$mega65/docs/sdcard-and-floppy-drive.md"),
    @('mega65', 'viciv-modes.md',                           "$mega65/docs/viciv-modes.md"),
    @('mega65', 'open-roms-STATUS.md',                      'https://raw.githubusercontent.com/MEGA65/open-roms/master/STATUS.md'),
    @('mega65', 'mega65-rom-faq.json',                      'POST https://files.mega65.org/php/readarticledetail.php articleid=145591dd-deb6-4bd0-aa89-8e39cd021470'),
    # --- Other emulators' sources, GPL-2.0: to read, not to copy (nothing of them is in Thing65) ---
    @('xemu-gpl', 'commodore_65.c',                         "$xemu/targets/c65/commodore_65.c"),
    @('xemu-gpl', 'dma65.c',                                "$xemu/targets/c65/dma65.c"),
    @('xemu-gpl', 'vic3.c',                                 "$xemu/targets/c65/vic3.c"),
    @('xemu-gpl', 'f011_core.c',                            "$xemu/xemu/f011_core.c"),
    @('xemu-gpl', 'd81access.c',                            "$xemu/xemu/d81access.c"),
    @('xemu-gpl', 'cpu65.c',                                "$xemu/xemu/cpu65.c")
)

$done = 0; $skipped = 0; $failed = @()
foreach ($entry in $files) {
    $folder = Join-Path $root $entry[0]
    New-Item -ItemType Directory -Force $folder | Out-Null
    $target = Join-Path $folder $entry[1]
    if ((Test-Path $target) -and -not $Force) { $skipped++; continue }
    try {
        if ($entry[2].StartsWith('POST ')) {
            # The ROM FAQ of the MEGA65 is the text of a JSON answer: keep the article as plain text
            $parts = $entry[2].Substring(5).Split(' ', 2)
            $json = Invoke-RestMethod -Method Post -Uri $parts[0] -Body $parts[1]
            $stripped = ($json[0].article -replace '<[^>]+>', "`n") -replace "(\r?\n)\s*(\r?\n)+", "`n"
            $text = [System.Net.WebUtility]::HtmlDecode($stripped)
            Set-Content -Path ($target -replace '\.json$', '.txt') -Value $text -Encoding UTF8
        } else {
            Invoke-WebRequest -Uri $entry[2] -OutFile $target -UserAgent 'Mozilla/5.0' -TimeoutSec 60
        }
        $done++
        Write-Host "OK    $($entry[0])/$($entry[1])"
        if ($target.EndsWith('.zip')) { Expand-Archive -Path $target -DestinationPath ($target -replace '\.zip$', '') -Force }
    } catch {
        $failed += "$($entry[0])/$($entry[1])  ($($_.Exception.Message))"
        Write-Host "FAIL  $($entry[0])/$($entry[1])"
    }
}
Write-Host "`nDownloaded $done, already there $skipped, failed $($failed.Count)"
$failed | ForEach-Object { Write-Host "  $_" }
