$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$installerDirectory = Join-Path $projectRoot 'installer'
$workDirectory = Join-Path $projectRoot ('obj\installer-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $installerDirectory,$workDirectory -Force | Out-Null
# Publish to a fresh directory so diagnostics and stale development files cannot ship.
& (Join-Path $projectRoot 'Build.ps1') -OutputDirectory (Join-Path $workDirectory 'App')
$payloadPath = Join-Path $workDirectory 'Payload.zip'
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $workDirectory 'App'), $payloadPath, [IO.Compression.CompressionLevel]::Optimal, $false)
& dotnet publish (Join-Path $projectRoot 'packaging\setup\RelayAlarm.Setup.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true "-p:PayloadPath=$payloadPath" -o (Join-Path $workDirectory 'Setup')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
$setupPath = Join-Path $installerDirectory 'RelayAlarm-Setup.exe'
Copy-Item -LiteralPath (Join-Path $workDirectory 'Setup\RelayAlarm-Setup.exe') -Destination $setupPath -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'packaging\INSTALL.txt') -Destination (Join-Path $installerDirectory 'INSTALL.txt') -Force
$hash = (Get-FileHash -LiteralPath $setupPath -Algorithm SHA256).Hash
Set-Content -LiteralPath (Join-Path $installerDirectory 'SHA256SUMS.txt') -Value "$hash  RelayAlarm-Setup.exe"
Write-Host "Ready to share: $setupPath"
