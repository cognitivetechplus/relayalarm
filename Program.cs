namespace RelayAlarm;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--diagnose"))
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"Executable: {Environment.ProcessPath}");
            try { report.AppendLine($"Package: {Windows.ApplicationModel.Package.Current.Id.FullName}"); }
            catch (Exception ex) { report.AppendLine($"Package error: {ex}"); }
            try
            {
                var listener = Windows.UI.Notifications.Management.UserNotificationListener.Current;
                var access = listener.GetAccessStatus();
                report.AppendLine($"Notification access: {access}");
                if (access == Windows.UI.Notifications.Management.UserNotificationListenerAccessStatus.Allowed)
                {
                    var notifications = listener.GetNotificationsAsync(Windows.UI.Notifications.NotificationKinds.Toast).AsTask().GetAwaiter().GetResult();
                    report.AppendLine($"Notification read: OK ({notifications.Count} currently available; content not recorded)");
                }
            }
            catch (Exception ex) { report.AppendLine($"Listener error: {ex}"); }
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "diagnostics.txt"), report.ToString());
            return;
        }
        if (!args.Contains("--smoke-test") && WindowsAppLaunch.RelaunchIfRegistered()) return;
        using var singleInstance = new Mutex(true, "Local\\RelayAlarm.Desktop", out var first);
        if (!first)
        {
            MessageBox.Show("Relay Alarm is already running. Look for its bell in the system tray.", "Relay Alarm");
            return;
        }
        Application.Run(new MainForm(args.Contains("--smoke-test")));
    }
}
