<#
.SYNOPSIS
    Downloads the external 6502 CPU test suites used by Thing65.Tests.

.DESCRIPTION
    - Klaus Dormann, 6502 functional test (GPL-3.0): 64 KB image and listing.
      https://github.com/Klaus2m5/6502_65C02_functional_tests
    - Tom Harte, SingleStepTests 65x02, folder 6502/v1 (MIT): 256 JSON files, one per opcode,
      each with 10,000 cases and the bus accesses cycle by cycle (about 1 GB in all).
      https://github.com/SingleStepTests/65x02

    The files go into src/Thing65.Tests/TestData/External, excluded from git: because of their licenses
    they are not part of the repository. Without the files the matching tests are "Inconclusive".

.PARAMETER SkipSingleStep
    Downloads only Klaus Dormann's test (a few KB instead of about 1 GB).

.PARAMETER Opcodes
    Downloads only the given opcodes of the SingleStepTests (e.g. "a9","8d").
#>
param(
    [switch]$SkipSingleStep,
    [string[]]$Opcodes
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root 'src/Thing65.Tests/TestData/External'

$dormann = Join-Path $target 'KlausDormann'
New-Item -ItemType Directory -Force $dormann | Out-Null
foreach ($file in '6502_functional_test.bin', '6502_functional_test.lst') {
    $url = "https://raw.githubusercontent.com/Klaus2m5/6502_65C02_functional_tests/master/bin_files/$file"
    Write-Host "Downloading $file"
    Invoke-WebRequest -Uri $url -OutFile (Join-Path $dormann $file)
}

if ($SkipSingleStep) { return }

$singleStep = Join-Path $target 'SingleStepTests/6502'
New-Item -ItemType Directory -Force $singleStep | Out-Null
$names = if ($Opcodes) { $Opcodes | ForEach-Object { $_.ToLowerInvariant() } } else { 0..255 | ForEach-Object { '{0:x2}' -f $_ } }
foreach ($name in $names) {
    $file = Join-Path $singleStep "$name.json"
    if (Test-Path $file) { continue }
    Write-Host "Downloading SingleStepTests 6502/v1/$name.json"
    Invoke-WebRequest -Uri "https://raw.githubusercontent.com/SingleStepTests/65x02/main/6502/v1/$name.json" -OutFile $file
}
