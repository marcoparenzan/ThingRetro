<#
.SYNOPSIS
    Packs the thing64 command line and installs it (or updates it) as a .NET global tool.

.DESCRIPTION
    The package goes into artifacts/ (excluded from git) and contains the desktop app (a C64 in a window)
    in its "desktop" folder, together with the C64 ROMs. The ROMs are copyrighted: keep the package for yourself,
    do not publish it on nuget.org.
    After the installation the command is available everywhere: thing64 --help
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'

dotnet pack (Join-Path $root 'src/Thing64/Thing64.csproj') -c Release -o $artifacts
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$installed = dotnet tool list -g | Select-String -Pattern '^thing64\s' -Quiet
if ($installed) { dotnet tool update -g Thing64 --add-source $artifacts }
else { dotnet tool install -g Thing64 --add-source $artifacts }
