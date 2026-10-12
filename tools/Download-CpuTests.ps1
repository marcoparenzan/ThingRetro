<#
.SYNOPSIS
    Scarica le suite di test esterne per la CPU 6502 usate da Fake64.Tests.

.DESCRIPTION
    - Klaus Dormann, 6502 functional test (GPL-3.0): immagine di 64 KB e listato.
      https://github.com/Klaus2m5/6502_65C02_functional_tests
    - Tom Harte, SingleStepTests 65x02, cartella 6502/v1 (MIT): 256 file JSON, uno per opcode,
      ciascuno con 10.000 casi e gli accessi al bus ciclo per ciclo (circa 1 GB in tutto).
      https://github.com/SingleStepTests/65x02

    I file finiscono in src/Fake64.Tests/TestData/External, esclusa da git: per le licenze
    non fanno parte del repository. Senza i file i test corrispondenti risultano "Inconclusive".

.PARAMETER SkipSingleStep
    Scarica solo il test di Klaus Dormann (pochi KB invece di circa 1 GB).

.PARAMETER Opcodes
    Scarica solo gli opcode indicati delle SingleStepTests (es. "a9","8d").
#>
param(
    [switch]$SkipSingleStep,
    [string[]]$Opcodes
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root 'src/Fake64.Tests/TestData/External'

$dormann = Join-Path $target 'KlausDormann'
New-Item -ItemType Directory -Force $dormann | Out-Null
foreach ($file in '6502_functional_test.bin', '6502_functional_test.lst') {
    $url = "https://raw.githubusercontent.com/Klaus2m5/6502_65C02_functional_tests/master/bin_files/$file"
    Write-Host "Scarico $file"
    Invoke-WebRequest -Uri $url -OutFile (Join-Path $dormann $file)
}

if ($SkipSingleStep) { return }

$singleStep = Join-Path $target 'SingleStepTests/6502'
New-Item -ItemType Directory -Force $singleStep | Out-Null
$names = if ($Opcodes) { $Opcodes | ForEach-Object { $_.ToLowerInvariant() } } else { 0..255 | ForEach-Object { '{0:x2}' -f $_ } }
foreach ($name in $names) {
    $file = Join-Path $singleStep "$name.json"
    if (Test-Path $file) { continue }
    Write-Host "Scarico SingleStepTests 6502/v1/$name.json"
    Invoke-WebRequest -Uri "https://raw.githubusercontent.com/SingleStepTests/65x02/main/6502/v1/$name.json" -OutFile $file
}
