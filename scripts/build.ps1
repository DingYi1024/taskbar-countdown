[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework 4.x compiler is required on Windows x64.' }
$out = Join-Path $project 'dist'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$refs = @('PresentationCore', 'PresentationFramework', 'WindowsBase') | ForEach-Object { '/r:' + (Join-Path $framework ('WPF\' + $_ + '.dll')) }
$stageDir = Join-Path $out '_stage'
New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
$tmpExe = "$stageDir\Countdown.exe"
$widgetExe = "$out\Countdown.exe"
$watchdogExe = "$out\CountdownWatchdog.exe"
& $compiler /nologo /target:winexe /optimize+ "/out:$tmpExe" "/win32icon:$project\assets\logo.ico" "/resource:$project\assets\logo.png,logo.png" "/resource:$project\src\Theme.xaml,Theme.xaml" /r:System.Web.Extensions.dll /r:System.Xaml.dll @refs "$project\src\Widget.cs" "$project\src\Settings.cs" "$project\src\ShareCard.cs"
if ($LASTEXITCODE -ne 0) { Remove-Item -LiteralPath $stageDir -Recurse -Force -ErrorAction SilentlyContinue; throw 'Widget build failed.' }
$watchdogRunning = @(Get-Process -Name CountdownWatchdog -ErrorAction SilentlyContinue | Where-Object { $_.Path -and ([string]::Equals($_.Path, $watchdogExe, [StringComparison]::OrdinalIgnoreCase)) }).Count -gt 0
if ($watchdogRunning) {
    Move-Item -LiteralPath $tmpExe -Destination "$out\Countdown.exe.pending" -Force
    Write-Host "Staged hot-update: $out\Countdown.exe.pending"
} else {
    Move-Item -LiteralPath $tmpExe -Destination $widgetExe -Force
    & $compiler /nologo /target:winexe /optimize+ "/out:$watchdogExe" "$project\src\Watchdog.cs"
    if ($LASTEXITCODE -ne 0) { Remove-Item -LiteralPath $stageDir -Recurse -Force -ErrorAction SilentlyContinue; throw 'Watchdog build failed.' }
}
Remove-Item -LiteralPath $stageDir -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item -LiteralPath "$project\config.example.json" -Destination $out
foreach ($name in @('start.cmd','settings.cmd','stop.cmd','install-startup.ps1','uninstall-startup.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $out
}
foreach ($name in @('README.md','LICENSE')) { Copy-Item -LiteralPath (Join-Path $project $name) -Destination $out }
Write-Host "Built: $out"
