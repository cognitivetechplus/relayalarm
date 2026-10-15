$ErrorActionPreference = 'Stop'
if (Get-Process -Name RelayAlarm -ErrorAction SilentlyContinue) {
    throw 'Exit Relay Alarm from its tray menu before installing or updating.'
}
$developerSettings = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -ErrorAction SilentlyContinue
if (!$developerSettings -or $developerSettings.AllowDevelopmentWithoutDevLicense -ne 1) {
    throw 'Enable Developer Mode in Windows Settings before installing.'
}
$installRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\RelayAlarm'
$versionDirectory = Join-Path $installRoot ('app-' + [Guid]::NewGuid().ToString('N'))
$previous = Get-AppxPackage -Name 'RelayAlarm.Desktop'
$registered = $false
try {
    New-Item -ItemType Directory -Path $versionDirectory -Force | Out-Null
    Copy-Item -Path (Join-Path $PSScriptRoot 'App\*') -Destination $versionDirectory -Recurse -Force
    Add-AppxPackage -Register (Join-Path $versionDirectory 'AppxManifest.xml') -ForceApplicationShutdown
    $registered = $true
    $package = Get-AppxPackage -Name 'RelayAlarm.Desktop'
    if (!$package -or $package.InstallLocation -ne $versionDirectory) { throw 'Windows did not register the installed app.' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall-User.ps1') -Destination (Join-Path $installRoot 'Uninstall.ps1') -Force
    $powershell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $uninstallCommand = '"' + $powershell + '" -NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $installRoot 'Uninstall.ps1') + '"'
    $registryPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\RelayAlarm'
    New-Item -Path $registryPath -Force | Out-Null
    $values = @{ DisplayName = 'Relay Alarm'; DisplayVersion = '1.0.0'; Publisher = 'Relay Alarm'; InstallLocation = $installRoot; DisplayIcon = (Join-Path $versionDirectory 'RelayAlarm.exe'); UninstallString = $uninstallCommand }
    foreach ($entry in $values.GetEnumerator()) { New-ItemProperty -Path $registryPath -Name $entry.Key -Value $entry.Value -PropertyType String -Force | Out-Null }
    foreach ($name in @('NoModify', 'NoRepair')) { New-ItemProperty -Path $registryPath -Name $name -Value 1 -PropertyType DWord -Force | Out-Null }
    Write-Output 'Installed. Open Relay Alarm from Start and allow notification access.'
} catch {
    $failure = $_
    # Restore the previous registration before removing a failed installation.
    if ($registered) {
        if ($previous -and (Test-Path -LiteralPath (Join-Path $previous.InstallLocation 'AppxManifest.xml'))) {
            Add-AppxPackage -Register (Join-Path $previous.InstallLocation 'AppxManifest.xml') -ForceApplicationShutdown
        } else { Get-AppxPackage -Name 'RelayAlarm.Desktop' | Remove-AppxPackage }
    }
    $resolvedRoot = [IO.Path]::GetFullPath($installRoot).TrimEnd('\') + '\'
    $resolvedVersion = [IO.Path]::GetFullPath($versionDirectory)
    if ($resolvedVersion.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolvedVersion)) {
        Remove-Item -LiteralPath $resolvedVersion -Recurse -Force
    }
    throw $failure
}
