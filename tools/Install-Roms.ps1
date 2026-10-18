<#
.SYNOPSIS
  Copies the system ROMs from Cloanto's C64 Forever (Free Express Edition) into ..\roms, with the names the project expects.
.DESCRIPTION
  The ROMs are protected by copyright and are not in the repository. C64 Forever Free Express Edition (personal use)
  installs them in C:\Users\Public\Documents\CBM Files\Shared\rom.
#>
param([string]$Source = 'C:\Users\Public\Documents\CBM Files\Shared\rom')

$roms = Join-Path $PSScriptRoot '..\roms'
New-Item -ItemType Directory -Force $roms | Out-Null
if (-not (Test-Path $Source)) { throw "ROM folder not found: $Source. Install C64 Forever Free Express Edition first." }

# source name -> name in roms\
$map = [ordered]@{
    'c-64-kernal.rom' = 'c-64-kernal.rom'
    'c-64-basic.rom' = 'c-64-basic.rom'
    'c-64-chargen.rom' = 'c-64-chargen.rom'
    'c-drive-1541.rom' = 'c-drive-1541.rom'
    'c-drive-1541-ii.rom' = 'c-drive-1541-ii.rom'
    'c-drive-1571.rom' = 'c-drive-1571.rom'
    'c-drive-1581.rom' = 'c-drive-1581.rom'
}
foreach ($name in $map.Keys) {
    $from = Join-Path $Source $name
    if (Test-Path $from) { Copy-Item $from (Join-Path $roms $map[$name]) -Force; "copied $name -> $($map[$name])" }
    else { Write-Warning "missing: $name" }
}
