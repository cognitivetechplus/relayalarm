using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Validate the exact distributable without installing or changing Windows settings.
        if (args.Length == 2 && args[0] == "--extract")
        {
            Extract(Path.GetFullPath(args[1]));
            return 0;
        }
        ApplicationConfiguration.Initialize();
        using var instance = new Mutex(true, "Local\\RelayAlarm.Setup", out var first);
        if (!first) return 1;
        using var form = new Form { Text = "Relay Alarm Setup", Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath), ClientSize = new Size(560, 435),
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false,
            StartPosition = FormStartPosition.CenterScreen, AutoScaleMode = AutoScaleMode.Dpi };
        var info = new Label { Left = 24, Top = 22, Width = 512, Height = 65,
            Text = "Install Relay Alarm for your Windows account.\n\nRequires x64 Windows 10 version 2004 or later. The .NET runtime is included." };
        var agreement = new TextBox { Left = 24, Top = 100, Width = 512, Height = 160,
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            Text = "Developer Mode agreement\r\n\r\nRelay Alarm needs Developer Mode to register its Windows notification-listener identity.\r\n\r\nIf it is off, Setup will ask Windows for administrator approval to enable it. This is a system-wide setting that permits development app registration, including unsigned app packages. Only install apps from sources you trust.\r\n\r\nDeveloper Mode stays enabled if installation fails or Relay Alarm is uninstalled. You can turn it off later in Windows Developer settings. Setup does not enable Device Portal or Device Discovery.\r\n\r\nAfter installation, open Relay Alarm from Start and allow notification access." };
        var consent = new CheckBox { Left = 24, Top = 274, Width = 512, Height = 45,
            Text = "I agree to enable Developer Mode if needed and install Relay Alarm.", Checked = false };
        var settings = new Button { Text = "Developer settings", Left = 24, Top = 337, Width = 155, Height = 32 };
        settings.Click += (_, _) => Process.Start(new ProcessStartInfo("ms-settings:developers") { UseShellExecute = true });
        var install = new Button { Text = "Install", Left = 326, Top = 337, Width = 100, Height = 32, Enabled = false };
        var close = new Button { Text = "Cancel", Left = 436, Top = 337, Width = 100, Height = 32 };
        close.Click += (_, _) => form.Close();
        var status = new Label { Left = 24, Top = 386, Width = 512, Height = 32 };
        var busy = false;
        consent.CheckedChanged += (_, _) => install.Enabled = consent.Checked && !busy;
        form.FormClosing += (_, e) => e.Cancel = busy;
        install.Click += async (_, _) =>
        {
            string? staging = null;
            try
            {
                if (!consent.Checked) throw new InvalidOperationException("Accept the agreement before installing.");
                if (RuntimeInformation.OSArchitecture != Architecture.X64 || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
                    throw new InvalidOperationException("This installer requires x64 Windows 10 version 2004 or later.");
                busy = true;
                install.Enabled = close.Enabled = settings.Enabled = consent.Enabled = false;
                if (!DeveloperModeEnabled())
                {
                    status.Text = "Waiting for administrator approval to enable Developer Mode…";
                    await EnableDeveloperModeAsync();
                }
                status.Text = "Installing…";
                staging = Path.Combine(Path.GetTempPath(), "RelayAlarm-Setup-" + Guid.NewGuid().ToString("N"));
                await Task.Run(() => Extract(staging));
                var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe"))
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(staging, "Install-User.ps1") })
                    start.ArgumentList.Add(arg);
                using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Windows PowerShell.");
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                var details = await output + "\n" + await error;
                if (process.ExitCode != 0) throw new InvalidOperationException(details.Trim());
                status.Text = "Installed. Open Relay Alarm from Start.";
                install.Visible = false;
                close.Text = "Finish";
            }
            catch (Exception ex)
            {
                status.Text = "Installation did not complete.";
                MessageBox.Show(form, ex.Message, "Relay Alarm Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                busy = false;
                install.Enabled = consent.Checked;
                close.Enabled = settings.Enabled = consent.Enabled = true;
                if (staging is not null)
                    try { Directory.Delete(staging, true); } catch { /* Temporary files can be removed later. */ }
            }
        };
        form.Controls.AddRange([info, agreement, consent, settings, install, close, status]);
        // Exercise consent gating without requesting elevation or installing anything.
        if (args.Length == 1 && args[0] == "--smoke-test")
        {
            if (consent.Checked || install.Enabled) return 1;
            consent.Checked = true;
            if (!install.Enabled) return 2;
            consent.Checked = false;
            if (install.Enabled) return 3;
            busy = true;
            consent.Checked = true;
            if (install.Enabled) return 4;
            return 0;
        }
        Application.Run(form);
        return 0;
    }

    private static bool DeveloperModeEnabled()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock");
        return key?.GetValue("AllowDevelopmentWithoutDevLicense") is int mode && mode == 1;
    }

    private static async Task EnableDeveloperModeAsync()
    {
        using var policy = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Appx");
        if (policy?.GetValue("AllowDevelopmentWithoutDevLicense") is int allowed && allowed == 0)
            throw new InvalidOperationException("Your organization blocks Developer Mode. Contact your administrator.");

        // Elevate only the fixed Windows setting change. App registration continues
        // in this original process so alternate administrator credentials cannot
        // cause the app to be installed for the administrator's account.
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\reg.exe"))
        { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var arg in new[] { "add", @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock", "/v", "AllowDevelopmentWithoutDevLicense", "/t", "REG_DWORD", "/d", "1", "/f", "/reg:64" })
            start.ArgumentList.Add(arg);
        Process process;
        try
        {
            process = Process.Start(start) ?? throw new InvalidOperationException("Could not request administrator approval.");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new InvalidOperationException("Administrator approval was canceled. Relay Alarm was not installed. You can retry or enable Developer Mode in Windows Settings.", ex);
        }
        using (process)
        {
            await process.WaitForExitAsync();
            if (process.ExitCode != 0 || !DeveloperModeEnabled())
                throw new InvalidOperationException("Windows could not enable Developer Mode. Check Developer settings or contact your administrator, then retry.");
        }
    }

    private static void Extract(string destination)
    {
        // Refuse an existing folder to avoid overwriting a caller's files.
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Extraction destination already exists.");
        Directory.CreateDirectory(destination);
        using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.zip")!;
        using var archive = new ZipArchive(payload, ZipArchiveMode.Read);
        archive.ExtractToDirectory(Path.Combine(destination, "App"));
        foreach (var name in new[] { "Install-User.ps1", "Uninstall-User.ps1" })
        {
            using var source = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)!;
            using var file = File.Create(Path.Combine(destination, name));
            source.CopyTo(file);
        }
    }
}
