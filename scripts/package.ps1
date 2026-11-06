[CmdletBinding()]
param([string]$Version = '1.0.0')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+([-.][A-Za-z0-9.-]+)?$') { throw 'Invalid version.' }
$project = Split-Path -Parent $PSScriptRoot
& "$PSScriptRoot\build.ps1"
& "$PSScriptRoot\test.ps1"
$artifacts = Join-Path $project 'artifacts'
$zip = Join-Path $artifacts "taskbar-countdown-v$Version-windows-x64.zip"
# Explicit allowlist prevents local configuration or logs entering a release.
$names = @('Countdown.exe','CountdownWatchdog.exe','config.example.json','start.cmd','settings.cmd','stop.cmd','install-startup.ps1','uninstall-startup.ps1','README.md','LICENSE')
$files = $names | ForEach-Object { Join-Path "$project\dist" $_ }
Compress-Archive -LiteralPath $files -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path -Leaf $zip)" | Set-Content -LiteralPath (Join-Path $artifacts 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Packaged: $zip"
