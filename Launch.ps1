$ErrorActionPreference = 'Stop'
$registeredApp = Get-AppxPackage -Name 'RelayAlarm.Desktop'
if (!$registeredApp) { throw 'Relay Alarm is not installed. Run Install.ps1 first.' }
Start-Process explorer.exe -ArgumentList "shell:AppsFolder\$($registeredApp.PackageFamilyName)!App"
