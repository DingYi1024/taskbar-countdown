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
& $compiler /nologo /target:winexe /optimize+ "/out:$out\Countdown.exe" "/win32icon:$project\assets\logo.ico" "/resource:$project\assets\logo.png,logo.png" "/resource:$project\src\Theme.xaml,Theme.xaml" /r:System.Web.Extensions.dll /r:System.Xaml.dll @refs "$project\src\Widget.cs" "$project\src\Settings.cs" "$project\src\ShareCard.cs"
if ($LASTEXITCODE -ne 0) { throw 'Widget build failed.' }
& $compiler /nologo /target:winexe /optimize+ "/out:$out\CountdownWatchdog.exe" "$project\src\Watchdog.cs"
if ($LASTEXITCODE -ne 0) { throw 'Watchdog build failed.' }
Copy-Item -LiteralPath "$project\config.example.json" -Destination $out
foreach ($name in @('start.cmd','settings.cmd','stop.cmd','install-startup.ps1','uninstall-startup.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $out
}
foreach ($name in @('README.md','LICENSE')) { Copy-Item -LiteralPath (Join-Path $project $name) -Destination $out }
Write-Host "Built: $out"
