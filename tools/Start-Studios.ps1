<#
.SYNOPSIS
  Starts the web studios (and, with -Web, the development servers of the web computers), each in a window of its own.
.DESCRIPTION
  Ports: 5064 Studio 64, 5065 Studio 65, 5164 Web 64, 5165 Web 65. A port that is already taken is left alone (Stop-Studios.ps1 frees them).
  The studios show the machine at /computer/ only after Publish-Web.ps1 has published it.
.PARAMETER Machine
  All (default), Thing64 or Thing65.
.PARAMETER Web
  Also starts the development server of the web computer (Debug, interpreted: slow, fine to try a change).
.PARAMETER NoBrowser
  Does not open the pages in the browser.
.EXAMPLE
  ./tools/Start-Studios.ps1 -Machine Thing65 -Web
#>
param(
    [ValidateSet('All', 'Thing64', 'Thing65')][string]$Machine = 'All',
    [switch]$Web,
    [switch]$NoBrowser
)

$root = Split-Path $PSScriptRoot
$machines = if ($Machine -eq 'All') { 'Thing64', 'Thing65' } else { @($Machine) }
$ports = @{ 'Thing64' = @{ Studio = 5064; Web = 5164 }; 'Thing65' = @{ Studio = 5065; Web = 5165 } }

function Test-Port($port) { [bool](Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) }

function Start-Server($name, $project, $port, $title) {
    if (Test-Port $port) { Write-Host "$title : port $port is already taken, left alone" -ForegroundColor Yellow; return }
    $directory = Join-Path $root "$name\src\$project"
    $command = "`$Host.UI.RawUI.WindowTitle = '$title ($port)'; dotnet run --launch-profile http"
    Start-Process powershell -ArgumentList '-NoExit', '-Command', $command -WorkingDirectory $directory
    Write-Host "$title : starting on http://localhost:$port/" -ForegroundColor Green
}

foreach ($name in $machines) {
    Start-Server $name "$name.WebStudio" $ports[$name].Studio "Studio $($name.Substring(5))"
    if ($Web) { Start-Server $name "$name.WebComputer" $ports[$name].Web "Web $($name.Substring(5))" }
}

if (-not $NoBrowser) {
    Start-Sleep -Seconds 20
    foreach ($name in $machines) {
        if (Test-Port $ports[$name].Studio) { Start-Process "http://localhost:$($ports[$name].Studio)/" }
        if ($Web -and (Test-Port $ports[$name].Web)) { Start-Process "http://localhost:$($ports[$name].Web)/" }
    }
}
