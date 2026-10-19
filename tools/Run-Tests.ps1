<#
.SYNOPSIS
  Runs the test suites of the repository and prints one line for each.
.PARAMETER Suite
  All (default), or one or more of: MOS, RetroEmu, Commodore, Thing64, Thing65.
.NOTES
  The suites that boot a ROM need the ROMs (Install-Roms.ps1). Commodore and Thing65 take a couple of minutes.
.EXAMPLE
  ./tools/Run-Tests.ps1 -Suite MOS, RetroEmu
#>
param([ValidateSet('All', 'MOS', 'RetroEmu', 'Commodore', 'Thing64', 'Thing65')][string[]]$Suite = @('All'))

$root = Split-Path $PSScriptRoot
$suites = [ordered]@{
    'MOS'       = 'ThingMOS\tests\ThingMOS.Tests'
    'RetroEmu'  = 'ThingRetroEmu\tests\ThingRetroEmu.Tests'
    'Commodore' = 'ThingCommodore\tests\ThingCommodore.Tests'
    'Thing64'   = 'Thing64\src\Thing64.Tests'
    'Thing65'   = 'Thing65\src\Thing65.Tests'
}
$selected = if ($Suite -contains 'All') { $suites.Keys } else { $Suite }

$failed = $false
foreach ($name in $selected) {
    Write-Host "== $name" -ForegroundColor Cyan
    $output = & dotnet test (Join-Path $root $suites[$name]) -nologo 2>&1
    $summary = $output | Where-Object { $_ -match 'Passed!|Failed!|error CS' } | Select-Object -First 5
    $summary | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) { $failed = $true; $output | Where-Object { $_ -match '^\s+Failed ' } | ForEach-Object { Write-Host $_ -ForegroundColor Red } }
}
if ($failed) { exit 1 }
