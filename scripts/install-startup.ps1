$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'CountdownWatchdog.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Run this script from the extracted release folder.' }
$shortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'Taskbar Countdown.lnk'
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($shortcut)
$link.TargetPath = $exe
$link.WorkingDirectory = $PSScriptRoot
$link.WindowStyle = 7
$link.Description = 'Taskbar Countdown'
$link.Save()
Write-Host 'Startup shortcut installed.'
