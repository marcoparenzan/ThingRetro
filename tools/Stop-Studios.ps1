<#
.SYNOPSIS
  Stops whatever listens on the ports of the studios and of the web computers (5064, 5065, 5164, 5165).
.EXAMPLE
  ./tools/Stop-Studios.ps1
#>
param([int[]]$Ports = @(5064, 5065, 5164, 5165))

foreach ($port in $Ports) {
    $connection = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $connection) { Write-Host "$port : nothing listening"; continue }
    $process = Get-Process -Id $connection.OwningProcess -ErrorAction SilentlyContinue
    if (-not $process) { continue }
    Stop-Process -Id $process.Id -Force
    Write-Host "$port : stopped $($process.ProcessName) ($($process.Id))" -ForegroundColor Green
}
