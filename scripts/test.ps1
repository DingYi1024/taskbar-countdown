$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out = Join-Path $project 'artifacts\tests'
New-Item -ItemType Directory -Force -Path $out | Out-Null
& $compiler /nologo "/out:$out\Tests.exe" "/r:$project\dist\Countdown.exe" "$project\tests\Tests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
Copy-Item -LiteralPath "$project\dist\Countdown.exe" -Destination $out
& "$out\Tests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
