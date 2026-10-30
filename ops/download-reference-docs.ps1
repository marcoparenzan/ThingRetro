param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'references'))
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$sources = @(
    @{ Name = 'mame-a7800-slot.cpp'; Url = 'https://raw.githubusercontent.com/mamedev/mame/master/src/devices/bus/a7800/a78_slot.cpp' },
    @{ Name = 'a7800-fork-slot.cpp'; Url = 'https://raw.githubusercontent.com/7800-devtools/a7800/master/src/devices/bus/a7800/a78_slot.cpp' },
    @{ Name = 'mame-maria.cpp'; Url = 'https://raw.githubusercontent.com/mamedev/mame/master/src/mame/atari/maria.cpp' },
    @{ Name = 'mame-license.txt'; Url = 'https://raw.githubusercontent.com/mamedev/mame/master/COPYING' }
)
$manifest = foreach ($source in $sources) {
    $path = Join-Path $OutputDirectory $source.Name
    Invoke-WebRequest -Uri $source.Url -OutFile $path
    [pscustomobject]@{
        File = $source.Name
        Url = $source.Url
        DownloadedUtc = [DateTime]::UtcNow.ToString('o')
        SHA256 = (Get-FileHash -Algorithm SHA256 -Path $path).Hash
    }
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory 'manifest.json')
Write-Host "References saved in $OutputDirectory"
