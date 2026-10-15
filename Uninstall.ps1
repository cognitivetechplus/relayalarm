$ErrorActionPreference = 'Stop'
Get-AppxPackage -Name 'RelayAlarm.Desktop' | Remove-AppxPackage
Write-Host 'Relay Alarm unregistered. The program files and sound preference have been kept.'
