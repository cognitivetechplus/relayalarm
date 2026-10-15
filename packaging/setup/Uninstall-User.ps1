$ErrorActionPreference = 'Stop'
try {
    if (Get-Process -Name RelayAlarm -ErrorAction SilentlyContinue) { throw 'Exit Relay Alarm from its tray menu before uninstalling.' }
    $expectedRoot = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\RelayAlarm')).TrimEnd('\')
    $actualRoot = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
    if ($actualRoot -ne $expectedRoot) { throw 'Run the uninstaller from the installed Relay Alarm folder.' }
    # Do not unregister a separate development copy installed by the owner.
    $package = Get-AppxPackage -Name 'RelayAlarm.Desktop'
    if ($package -and $package.InstallLocation.StartsWith($actualRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        Remove-AppxPackage -Package $package.PackageFullName
    }
    Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\RelayAlarm' -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $actualRoot -Recurse -Force
    Add-Type -AssemblyName System.Windows.Forms
    [Windows.Forms.MessageBox]::Show('Relay Alarm has been removed. Your sound preferences were kept.', 'Relay Alarm') | Out-Null
} catch {
    Add-Type -AssemblyName System.Windows.Forms
    [Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Relay Alarm uninstall failed') | Out-Null
    exit 1
}
