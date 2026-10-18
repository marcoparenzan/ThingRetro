<#
.SYNOPSIS
  Installs the system ROMs for every machine of the repository (from Cloanto's C64 Forever Free Express Edition).
#>
param([string]$Source = 'C:\Users\Public\Documents\CBM Files\Shared\rom')

foreach ($folder in 'Thing64', 'Thing65', 'ThingCommodore') {
    $script = Join-Path $PSScriptRoot "..\$folder\tools\Install-Roms.ps1"
    if (Test-Path $script) { "== $folder"; & $script -Source $Source }
}
