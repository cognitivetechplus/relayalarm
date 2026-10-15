$ErrorActionPreference = 'Stop'
$registeredApp = Get-AppxPackage -Name 'RelayAlarm.Desktop'
if (!$registeredApp) { throw 'Relay Alarm is not installed. Run Install.ps1 first.' }
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class RelayAlarmActivation
{
    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appId,
            [MarshalAs(UnmanagedType.LPWStr)] string arguments, uint options, out uint processId);
    }
    public static uint Activate(string appId, string arguments)
    {
        var manager = (IApplicationActivationManager)Activator.CreateInstance(
            Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")));
        try
        {
            uint processId;
            Marshal.ThrowExceptionForHR(manager.ActivateApplication(appId, arguments, 2, out processId));
            return processId;
        }
        finally { Marshal.ReleaseComObject(manager); }
    }
}
'@
$diagnosticId = [RelayAlarmActivation]::Activate("$($registeredApp.PackageFamilyName)!App", '--diagnose')
$diagnosticProcess = Get-Process -Id $diagnosticId -ErrorAction SilentlyContinue
if ($diagnosticProcess -and !$diagnosticProcess.WaitForExit(15000)) { throw 'Diagnostics timed out.' }
Get-Content -LiteralPath (Join-Path $registeredApp.InstallLocation 'diagnostics.txt')
