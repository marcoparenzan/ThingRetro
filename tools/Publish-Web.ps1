<#
.SYNOPSIS
  Publishes the web computers (WebAssembly) of Thing64 and Thing65 to <machine>\artifacts\web, where the studios serve them at /computer/.
.DESCRIPTION
  Release is compiled ahead of time (AOT): it needs the wasm-tools workload (dotnet workload install wasm-tools) and takes a few minutes.
  The studios have to be restarted after the first publish (Stop-Studios.ps1, Start-Studios.ps1).
.PARAMETER Machine
  All (default), Thing64 or Thing65.
.PARAMETER NoAot
  Publishes without the ahead-of-time compilation: a minute instead of minutes, far too slow to play, enough to try something.
.PARAMETER Clean
  Deletes the previous publish first: dotnet publish -o does not remove the files of an earlier one (old .wasm files with other hashes stay).
.EXAMPLE
  ./tools/Publish-Web.ps1 -Machine Thing65 -Clean
#>
param(
    [ValidateSet('All', 'Thing64', 'Thing65')][string]$Machine = 'All',
    [switch]$NoAot,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$machines = if ($Machine -eq 'All') { 'Thing64', 'Thing65' } else { @($Machine) }

foreach ($name in $machines) {
    $project = Join-Path $root "$name\src\$name.WebComputer"
    $output = Join-Path $root "$name\artifacts\web"
    if ($Clean -and (Test-Path $output)) { Remove-Item $output -Recurse -Force }
    Write-Host "== Publishing $name -> $output" -ForegroundColor Cyan
    $arguments = @('publish', $project, '-c', 'Release', '-o', $output, '-nologo', '-v', 'q')
    if ($NoAot) { $arguments += '-p:RunAOTCompilation=false' }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "The publish of $name failed (exit code $LASTEXITCODE)" }
    $wasm = @(Get-ChildItem (Join-Path $output 'wwwroot\_framework') -Filter *.wasm -ErrorAction SilentlyContinue).Count
    Write-Host "$name published: $wasm .wasm files." -ForegroundColor Green
}
