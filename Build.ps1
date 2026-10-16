param([switch]$FrameworkDependent, [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist\RelayAlarm'))
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
& dotnet publish (Join-Path $projectRoot 'RelayAlarm.csproj') -c Release -r win-x64 --self-contained $selfContained -o $outputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'packaging\AppxManifest.xml') -Destination $outputDirectory -Force
$assetDirectory = Join-Path $outputDirectory 'Assets'
New-Item -ItemType Directory -Path $assetDirectory -Force | Out-Null
foreach ($asset in @('StoreLogo.png', 'Logo44.png', 'Logo150.png')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot "Assets\$asset") -Destination $assetDirectory -Force
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'Install.ps1'),(Join-Path $projectRoot 'Uninstall.ps1'),(Join-Path $projectRoot 'Launch.ps1'),(Join-Path $projectRoot 'Diagnose.ps1') -Destination $outputDirectory -Force
Write-Host "Built: $outputDirectory"
Write-Host 'Run Install.ps1 in that directory to register the app, then open Relay Alarm from Start.'
