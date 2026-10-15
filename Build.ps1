param([switch]$FrameworkDependent, [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist\RelayAlarm'))
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
& dotnet publish (Join-Path $projectRoot 'RelayAlarm.csproj') -c Release -r win-x64 --self-contained $selfContained -o $outputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'packaging\AppxManifest.xml') -Destination $outputDirectory -Force
$assetDirectory = Join-Path $outputDirectory 'Assets'
New-Item -ItemType Directory -Path $assetDirectory -Force | Out-Null
Add-Type -AssemblyName System.Drawing
foreach ($asset in @(@('StoreLogo',50), @('Logo44',44), @('Logo150',150))) {
    $size = [int]$asset[1]
    $bitmap = New-Object System.Drawing.Bitmap $size,$size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::FromArgb(24,35,56))
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,194,71))
    $graphics.FillEllipse($brush, [single]($size*0.25), [single]($size*0.18), [single]($size*0.5), [single]($size*0.6))
    $graphics.FillRectangle($brush, [single]($size*0.19), [single]($size*0.65), [single]($size*0.62), [single]($size*0.1))
    $graphics.FillEllipse($brush, [single]($size*0.43), [single]($size*0.8), [single]($size*0.14), [single]($size*0.1))
    $bitmap.Save((Join-Path $assetDirectory ($asset[0] + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
    $brush.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'Install.ps1'),(Join-Path $projectRoot 'Uninstall.ps1'),(Join-Path $projectRoot 'Launch.ps1'),(Join-Path $projectRoot 'Diagnose.ps1') -Destination $outputDirectory -Force
Write-Host "Built: $outputDirectory"
Write-Host 'Run Install.ps1 in that directory to register the app, then open Relay Alarm from Start.'
