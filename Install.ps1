$ErrorActionPreference = 'Stop'
$manifestPath = Join-Path $PSScriptRoot 'AppxManifest.xml'
if (!(Test-Path -LiteralPath $manifestPath)) {
    $manifestPath = Join-Path $PSScriptRoot 'dist\RelayAlarm\AppxManifest.xml'
}
if (!(Test-Path -LiteralPath $manifestPath)) { throw 'Run .\Build.ps1 first.' }
$developerSettings = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -ErrorAction SilentlyContinue
if (!$developerSettings -or $developerSettings.AllowDevelopmentWithoutDevLicense -ne 1) {
    throw 'This local development build needs Developer Mode. Enable it in Windows Settings (search for Developer Mode), then run Install.ps1 again. This script does not change that setting.'
}
if (Get-Process -Name RelayAlarm -ErrorAction SilentlyContinue) { throw 'Exit Relay Alarm from its tray menu before installing.' }
Add-AppxPackage -Register $manifestPath -ForceApplicationShutdown
Write-Host 'Installed. Open Relay Alarm from Start and allow notification access.'
