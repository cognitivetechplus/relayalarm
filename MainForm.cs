using System.Text.Json;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace RelayAlarm;

internal sealed class MainForm : Form
{
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(520, 0), Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 10), Text = "Ready to connect to Windows notifications." };
    private readonly Label soundLabel = new() { AutoSize = true, MaximumSize = new Size(520, 0), Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 10) };
    private readonly Button stop = new() { Text = "Stop", Dock = DockStyle.Top, Height = 90, Enabled = false, Font = new Font("Segoe UI", 24, FontStyle.Bold), BackColor = Color.FromArgb(196, 48, 59), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
    private readonly Button connect = new() { Text = "Enable notification access", AutoSize = true };
    private readonly System.Windows.Forms.Timer poll = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer repeat = new() { Interval = 2500 };
    private readonly NotifyIcon tray;
    private readonly AlarmSound sound = new();
    private readonly NotificationTracker tracker = new();
    private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RelayAlarm", "settings.json");
    private UserNotificationListener? listener;
    private bool polling, alarming, quitting;

    public MainForm(bool smokeTest)
    {
        Text = "Relay Alarm";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.FromArgb(230, 230, 230);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(580, 530);
        MinimumSize = new Size(600, 570);
        Font = new Font("Segoe UI", 11);
        StartPosition = FormStartPosition.CenterScreen;
        var header = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(45, 45, 48) };
        var caption = new Label { Text = "Relay Alarm", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 0, 0, 0), Font = new Font("Segoe UI", 10) };
        var minimize = new Button { Text = "—", Dock = DockStyle.Right, Width = 46, TabStop = false, AccessibleName = "Minimize" };
        var close = new Button { Text = "×", Dock = DockStyle.Right, Width = 46, TabStop = false, AccessibleName = "Close to tray", Font = new Font("Segoe UI", 16) };
        StyleButton(minimize, header.BackColor);
        StyleButton(close, header.BackColor);
        close.FlatAppearance.MouseOverBackColor = Color.FromArgb(196, 48, 59);
        minimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        close.Click += (_, _) => Close();
        header.MouseDown += DragWindow;
        caption.MouseDown += DragWindow;
        header.Controls.Add(caption);
        var logo = new PictureBox { Image = Icon?.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Left, Width = 40, Padding = new Padding(6), AccessibleName = "Alarm Relay logo" };
        logo.MouseDown += DragWindow;
        logo.Disposed += (_, _) => logo.Image?.Dispose();
        header.Controls.Add(logo);
        header.Controls.Add(minimize);
        header.Controls.Add(close);
        var title = new Label { Text = "Never miss an Outlook alert", Font = new Font("Segoe UI", 18, FontStyle.Bold), AutoSize = true, Dock = DockStyle.Fill };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 12, 0, 12) };
        var test = new Button { Text = "Test sound", AutoSize = true };
        var choose = new Button { Text = "Choose WAV…", AutoSize = true };
        var reset = new Button { Text = "Windows sound", AutoSize = true };
        foreach (var button in new[] { test, choose, reset, connect })
        {
            StyleButton(button, Color.FromArgb(51, 51, 55));
            button.Padding = new Padding(10, 7, 10, 7);
            button.Margin = new Padding(0, 0, 8, 8);
        }
        stop.FlatAppearance.BorderSize = 0;
        stop.BackColor = Color.FromArgb(48, 48, 52);
        stop.EnabledChanged += (_, _) => stop.BackColor = stop.Enabled
            ? Color.FromArgb(196, 48, 59) : Color.FromArgb(48, 48, 52);
        stop.FlatAppearance.MouseOverBackColor = Color.FromArgb(215, 58, 70);
        status.ForeColor = Color.FromArgb(190, 190, 195);
        soundLabel.ForeColor = Color.FromArgb(190, 190, 195);
        buttons.Controls.AddRange([test, choose, reset, connect]);
        var hint = new Label { AutoSize = true, Dock = DockStyle.Fill, Text = "Closing this window keeps listening in the tray.\nRight-click the tray icon to exit." };
        hint.ForeColor = Color.FromArgb(155, 155, 162);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, AutoScroll = true, Padding = new Padding(24) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 6; row++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(status, 0, 1);
        layout.Controls.Add(stop, 0, 2);
        layout.Controls.Add(soundLabel, 0, 3);
        layout.Controls.Add(buttons, 0, 4);
        layout.Controls.Add(hint, 0, 5);
        Controls.Add(layout);
        Controls.Add(header);
        var menu = new ContextMenuStrip { BackColor = Color.FromArgb(45, 45, 48), ForeColor = ForeColor, Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()) };
        menu.Items.Add("Open", null, (_, _) => ShowWindow());
        menu.Items.Add("Stop", null, (_, _) => StopAlarm());
        menu.Items.Add("Exit", null, (_, _) => { quitting = true; Close(); });
        tray = new NotifyIcon { Icon = Icon, Text = "Relay Alarm", Visible = true, ContextMenuStrip = menu };
        tray.DoubleClick += (_, _) => ShowWindow();
        stop.Click += (_, _) => StopAlarm();
        test.Click += (_, _) => StartAlarm();
        choose.Click += (_, _) => ChooseSound();
        reset.Click += (_, _) => SetSound(null);
        connect.Click += async (_, _) => await ConnectAsync();
        repeat.Tick += (_, _) => sound.Play();
        poll.Tick += async (_, _) => await PollAsync();
        FormClosing += (_, e) =>
        {
            if (!quitting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        };
        LoadSound();
        Shown += async (_, _) =>
        {
            if (smokeTest)
            {
                StartAlarm(play: false);
                if (!alarming || !stop.Enabled || !repeat.Enabled) throw new InvalidOperationException("Alarm did not start.");
                StopAlarm();
                if (alarming || stop.Enabled || repeat.Enabled) throw new InvalidOperationException("Alarm did not stop.");
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-test.txt"), "PASS: window, alarm start, Stop, timer shutdown.");
                quitting = true;
                Close();
                return;
            }
            await ConnectAsync();
        };
    }

    private static void StyleButton(Button button, Color background)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.BackColor = background;
        button.ForeColor = Color.FromArgb(230, 230, 230);
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(65, 65, 70);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(80, 80, 86);
        button.Cursor = Cursors.Hand;
    }

    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero); // Drag the custom title bar like a native caption.
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(45, 45, 48);
        public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
        public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
        public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;
        public override Color MenuItemSelected => Color.FromArgb(65, 65, 70);
        public override Color MenuItemBorder => MenuItemSelected;
        public override Color MenuBorder => ToolStripDropDownBackground;
    }

    private async Task ConnectAsync()
    {
        connect.Enabled = false;
        var stage = "Windows package identity";
        try
        {
            _ = Package.Current.Id;
            stage = "notification listener";
            listener = UserNotificationListener.Current;
            stage = "notification permission";
            var access = listener.GetAccessStatus();
            if (access == UserNotificationListenerAccessStatus.Unspecified)
                access = await listener.RequestAccessAsync();
            if (access != UserNotificationListenerAccessStatus.Allowed)
            {
                status.Text = "Notification access is needed. Allow Relay Alarm in Windows Settings > Privacy & security > Notifications, then retry.";
                return;
            }
            await PollAsync(); // Establish a baseline: existing notifications never ring on startup.
            poll.Start();
        }
        catch (Exception ex)
        {
            status.Text = stage == "Windows package identity"
                ? "This copy was opened without its Windows app identity. Exit from the tray, then open Relay Alarm from Start (or run Launch.ps1)."
                : $"Could not enable {stage}: {ex.Message} (0x{ex.HResult:X8}). Click Enable notification access to retry.";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(settingsPath)!, "connection-error.txt"), $"Stage: {stage}\nExecutable: {Environment.ProcessPath}\n{ex}");
            }
            catch { /* Error reporting must not interrupt the app. */ }
        }
        finally { connect.Enabled = true; }
    }

    private async Task PollAsync()
    {
        if (polling || listener is null || IsDisposed) return;
        polling = true;
        try
        {
            if (listener.GetAccessStatus() != UserNotificationListenerAccessStatus.Allowed)
            {
                poll.Stop();
                status.Text = "Notification access was revoked. Enable it in Windows Settings and reconnect.";
                return;
            }
            var notifications = await listener.GetNotificationsAsync(NotificationKinds.Toast);
            if (IsDisposed) return;
            var snapshot = new List<(string Key, bool IsOutlook)>();
            foreach (var item in notifications)
            {
                try
                {
                    var appId = item.AppInfo.AppUserModelId;
                    snapshot.Add(($"{appId}:{item.Id}:{item.CreationTime.UtcTicks}", appId.StartsWith("Microsoft.OutlookForWindows_", StringComparison.OrdinalIgnoreCase)));
                }
                catch { /* A notification can disappear while its metadata is read. */ }
            }
            if (tracker.Update(snapshot)) StartAlarm();
            if (!alarming) status.Text = "Listening for new Outlook notifications…";
        }
        catch (Exception ex) { if (!IsDisposed) status.Text = "Unable to read notifications: " + ex.Message; }
        finally { polling = false; }
    }

    private void StartAlarm(bool play = true)
    {
        if (alarming) return;
        alarming = true;
        stop.Enabled = true;
        status.Text = "Notification received. Sound repeats until you press Stop.";
        TopMost = true;
        ShowWindow();
        if (play) sound.Play();
        repeat.Start();
    }

    private void StopAlarm()
    {
        repeat.Stop();
        sound.Stop();
        alarming = false;
        stop.Enabled = false;
        TopMost = false;
        status.Text = poll.Enabled ? "Listening for new Outlook notifications…" : "Alarm stopped. Enable notification access to listen.";
    }

    private void ShowWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }

    private void ChooseSound()
    {
        using var dialog = new OpenFileDialog { Filter = "WAV audio (*.wav)|*.wav", Title = "Choose a notification sound" };
        if (dialog.ShowDialog(this) == DialogResult.OK) SetSound(dialog.FileName);
    }

    private void SetSound(string? path)
    {
        try
        {
            sound.SetFile(path);
            soundLabel.Text = path is null ? "Sound: Windows notification sound" : "Sound: " + Path.GetFileName(path);
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(path));
            if (alarming) sound.Play();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Cannot set sound"); }
    }

    private void LoadSound()
    {
        soundLabel.Text = "Sound: Windows notification sound";
        try
        {
            if (!File.Exists(settingsPath)) return;
            var path = JsonSerializer.Deserialize<string?>(File.ReadAllText(settingsPath));
            sound.SetFile(path);
            if (path is not null) soundLabel.Text = "Sound: " + Path.GetFileName(path);
        }
        catch { status.Text = "Saved sound unavailable; using the Windows notification sound."; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { poll.Dispose(); repeat.Dispose(); tray.Dispose(); tray.ContextMenuStrip?.Dispose(); sound.Dispose(); }
        base.Dispose(disposing);
    }
}
