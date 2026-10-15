using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace RelayAlarm;

internal static class WindowsAppLaunch
{
    public static bool RelaunchIfRegistered()
    {
        try { _ = Package.Current.Id; return false; }
        catch (InvalidOperationException) { }
        try
        {
            var package = new PackageManager().FindPackagesForUser(string.Empty)
                .FirstOrDefault(p => p.Id.Name == "RelayAlarm.Desktop" && p.Id.Publisher == "CN=RelayAlarm");
            if (package is null) return false;
            var manager = (IApplicationActivationManager)Activator.CreateInstance(
                Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"))!)!;
            try
            {
                Marshal.ThrowExceptionForHR(manager.ActivateApplication(package.Id.FamilyName + "!App", "", 0, out _));
                return true;
            }
            finally { Marshal.ReleaseComObject(manager); }
        }
        catch { return false; } // The main window provides setup instructions if activation fails.
    }

    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        [PreserveSig]
        int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appId,
            [MarshalAs(UnmanagedType.LPWStr)] string arguments, uint options, out uint processId);
    }
}
