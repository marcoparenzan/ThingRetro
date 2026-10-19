<#
.SYNOPSIS
  Packs the command lines thing64 and thing65 (with the desktop app inside) and installs them as .NET global tools, replacing the installed ones.
.DESCRIPTION
  The package contains the ROMs of the roms folder: keep it for yourself. Run it again after every change to the desktop apps, otherwise
  `thing64 run` starts the old app.
.PARAMETER Machine
  All (default), Thing64 or Thing65.
.EXAMPLE
  ./tools/Install-Tool.ps1 -Machine Thing65
#>
param([ValidateSet('All', 'Thing64', 'Thing65')][string]$Machine = 'All')

$root = Split-Path $PSScriptRoot
$machines = if ($Machine -eq 'All') { 'Thing64', 'Thing65' } else { @($Machine) }

foreach ($name in $machines) {
    Write-Host "== $name" -ForegroundColor Cyan
    $project = Join-Path $root "$name\src\$name\$name.csproj"
    $artifacts = Join-Path $root "$name\artifacts"
    & dotnet pack $project -c Release -nologo -v q
    if ($LASTEXITCODE -ne 0) { Write-Host "$name : pack failed" -ForegroundColor Red; exit 1 }
    & dotnet tool uninstall -g $name 2>$null | Out-Null
    & dotnet tool install -g $name --add-source $artifacts
    if ($LASTEXITCODE -ne 0) { Write-Host "$name : install failed" -ForegroundColor Red; exit 1 }
}
