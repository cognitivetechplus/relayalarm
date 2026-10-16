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
    private readonly Button stop = new StopButton { Text = "Stop", Dock = DockStyle.Top, Height = 90, Enabled = false, Font = new Font("Segoe UI", 24, FontStyle.Bold), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
    private readonly Button connect = new() { Text = "Enable notification access", AutoSize = true };
    private readonly RadioButton alarmOn = new() { Text = "On", AutoSize = true, AccessibleName = "Alarm on", Margin = new Padding(0, 8, 12, 0) };
    private readonly RadioButton alarmOff = new() { Text = "Off", AutoSize = true, AccessibleName = "Alarm off", Margin = new Padding(0, 8, 16, 0) };
    private readonly Button test = new() { Text = "Test sound", AutoSize = true };
    private readonly ComboBox duration = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, AccessibleName = "Alarm duration" };
    private readonly System.Windows.Forms.Timer alarmTimeout = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer poll = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer repeat = new() { Interval = 2500 };
    private readonly NotifyIcon tray;
    private readonly AlarmSound sound = new();
    private readonly NotificationTracker tracker = new();
    private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RelayAlarm", "settings.json");
    private UserNotificationListener? listener;
    private bool polling, alarming, quitting;
    private bool alarmEnabled = true;
    private int monitoringVersion;
    private DateTimeOffset alarmEndsAt;
    private sealed record Settings(string? SoundPath, bool AlarmEnabled = true, int DurationMinutes = 5);

    public MainForm(bool smokeTest)
    {
        Text = "Relay Alarm";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.FromArgb(230, 230, 230);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(580, 610);
        MinimumSize = new Size(600, 650);
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
        var choose = new Button { Text = "Choose WAV…", AutoSize = true };
        var reset = new Button { Text = "Windows sound", AutoSize = true };
        foreach (var button in new[] { test, choose, reset, connect })
        {
            StyleButton(button, Color.FromArgb(51, 51, 55));
            button.Padding = new Padding(10, 7, 10, 7);
            button.Margin = new Padding(0, 0, 8, 8);
        }
        stop.UseVisualStyleBackColor = false;
        stop.FlatAppearance.BorderSize = 2;
        stop.FlatAppearance.BorderColor = Color.FromArgb(244, 110, 121);
        stop.BackColor = Color.FromArgb(122, 42, 51);
        stop.EnabledChanged += (_, _) => stop.BackColor = stop.Enabled
            ? Color.FromArgb(218, 45, 63) : Color.FromArgb(122, 42, 51);
        stop.FlatAppearance.MouseOverBackColor = Color.FromArgb(238, 65, 82);
        stop.FlatAppearance.MouseDownBackColor = Color.FromArgb(183, 32, 48);
        stop.Cursor = Cursors.Hand;
        status.ForeColor = Color.FromArgb(190, 190, 195);
        soundLabel.ForeColor = Color.FromArgb(190, 190, 195);
        buttons.Controls.AddRange([test, choose, reset, connect]);
        duration.Items.AddRange(["5 mins", "10 mins", "15 mins"]);
        duration.SelectedIndex = 0;
        var alarmControls = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 10, 0, 4) };
        alarmControls.Controls.AddRange([
            new Label { Text = "Alarm:", AutoSize = true, Margin = new Padding(0, 9, 10, 0) },
            alarmOn, alarmOff,
            new Label { Text = "Ring for:", AutoSize = true, Margin = new Padding(4, 9, 8, 0) }, duration]);
        var hint = new Label { AutoSize = true, Dock = DockStyle.Fill, Text = "Closing this window keeps listening in the tray.\nRight-click the tray icon to exit." };
        hint.ForeColor = Color.FromArgb(155, 155, 162);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, AutoScroll = true, Padding = new Padding(24) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 7; row++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(status, 0, 1);
        layout.Controls.Add(alarmControls, 0, 2);
        layout.Controls.Add(stop, 0, 3);
        layout.Controls.Add(soundLabel, 0, 4);
        layout.Controls.Add(buttons, 0, 5);
        layout.Controls.Add(hint, 0, 6);
        Controls.Add(layout);
        Controls.Add(header);
        var menu = new ContextMenuStrip { BackColor = Color.FromArgb(45, 45, 48), ForeColor = ForeColor, Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()) };
        menu.Items.Add("Open", null, (_, _) => ShowWindow());
        menu.Items.Add("Stop", null, (_, _) => StopAlarm());
        menu.Items.Add("Exit", null, (_, _) => { quitting = true; Close(); });
        tray = new NotifyIcon { Icon = Icon, Text = "Relay Alarm", Visible = true, ContextMenuStrip = menu };
        tray.DoubleClick += (_, _) => ShowWindow();
        stop.Click += (_, _) => StopAlarm();
        duration.SelectedIndexChanged += (_, _) => SaveSettings();
        test.Click += (_, _) => StartAlarm();
        choose.Click += (_, _) => ChooseSound();
        reset.Click += (_, _) => SetSound(null);
        connect.Click += async (_, _) => await ConnectAsync();
        repeat.Tick += (_, _) =>
        {
            ExpireAlarm(DateTimeOffset.UtcNow);
            if (alarming) sound.Play();
        };
        poll.Tick += async (_, _) => await PollAsync();
        alarmTimeout.Tick += (_, _) => ExpireAlarm(DateTimeOffset.UtcNow);
        FormClosing += (_, e) =>
        {
            if (!quitting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        };
        LoadSettings();
        alarmOn.Checked = alarmEnabled;
        alarmOff.Checked = !alarmEnabled;
        test.Enabled = alarmEnabled;
        alarmOn.CheckedChanged += (_, _) =>
        {
            if (alarmOn.Checked && !alarmEnabled) SetAlarmEnabled(true);
        };
        alarmOff.CheckedChanged += (_, _) =>
        {
            if (alarmOff.Checked && alarmEnabled) SetAlarmEnabled(false);
        };
        Shown += async (_, _) =>
        {
            if (smokeTest)
            {
                loadingSettings = true; // Verification must not overwrite the user's preferences.
                alarmEnabled = true;
                StartAlarm(play: false);
                if (!alarming || !stop.Enabled || !repeat.Enabled || !alarmTimeout.Enabled) throw new InvalidOperationException("Alarm did not start.");
                ExpireAlarm(alarmEndsAt.AddSeconds(-1));
                if (!alarming) throw new InvalidOperationException("Alarm expired too early.");
                ExpireAlarm(alarmEndsAt);
                if (alarming || stop.Enabled || repeat.Enabled || alarmTimeout.Enabled) throw new InvalidOperationException("Alarm did not expire.");
                StartAlarm(play: false);
                StopAlarm();
                if (alarming || stop.Enabled || repeat.Enabled || alarmTimeout.Enabled) throw new InvalidOperationException("Alarm did not stop.");
                StartAlarm(play: false);
                SetAlarmEnabled(false, save: false);
                if (!alarmOff.Checked || alarmOn.Checked || test.Enabled) throw new InvalidOperationException("Off selection is inconsistent.");
                StartAlarm(play: false);
                if (alarming || repeat.Enabled || alarmTimeout.Enabled) throw new InvalidOperationException("Disabled alarm started or kept ringing.");
                SetAlarmEnabled(true, save: false);
                if (!alarmOn.Checked || alarmOff.Checked || !test.Enabled) throw new InvalidOperationException("On selection is inconsistent.");
                foreach (var index in new[] { 0, 1, 2 })
                {
                    duration.SelectedIndex = index;
                    var before = DateTimeOffset.UtcNow;
                    StartAlarm(play: false);
                    if (alarmEndsAt < before.AddMinutes((index + 1) * 5) || alarmEndsAt > DateTimeOffset.UtcNow.AddMinutes((index + 1) * 5)) throw new InvalidOperationException("Wrong alarm duration.");
                    StopAlarm();
                }
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-test.txt"), "PASS: window, alarm start, Stop, automatic expiry, enable/disable, 5/10/15-minute durations, timer shutdown.");
                quitting = true;
                Close();
                return;
            }
            await ConnectAsync();
        };
    }

    private sealed class StopButton : Button
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Enabled)
            {
                base.OnPaint(e);
                return;
            }

            // WinForms otherwise paints disabled text black against the dark background.
            e.Graphics.Clear(BackColor);
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle, FlatAppearance.BorderColor, ButtonBorderStyle.Solid);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
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
            if (!alarming) UpdateIdleStatus();
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
        var version = monitoringVersion;
        try
        {
            if (listener.GetAccessStatus() != UserNotificationListenerAccessStatus.Allowed)
            {
                poll.Stop();
                status.Text = "Notification access was revoked. Enable it in Windows Settings and reconnect.";
                return;
            }
            var notifications = await listener.GetNotificationsAsync(NotificationKinds.Toast);
            if (IsDisposed || version != monitoringVersion) return;
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
            if (tracker.Update(snapshot) && alarmEnabled) StartAlarm();
            if (!alarming) UpdateIdleStatus();
        }
        catch (Exception ex) { if (!IsDisposed) status.Text = "Unable to read notifications: " + ex.Message; }
        finally { polling = false; }
    }

    private void StartAlarm(bool play = true)
    {
        if (alarming || !alarmEnabled) return;
        alarming = true;
        stop.Enabled = true;
        var minutes = (duration.SelectedIndex + 1) * 5;
        alarmEndsAt = DateTimeOffset.UtcNow.AddMinutes(minutes);
        status.Text = $"Sound repeats for {minutes} minutes or until you press Stop. Duration changes apply to the next alarm.";
        TopMost = true;
        ShowWindow();
        if (play) sound.Play();
        repeat.Start();
        alarmTimeout.Start();
    }

    private void StopAlarm()
    {
        repeat.Stop();
        alarmTimeout.Stop();
        sound.Stop();
        alarming = false;
        stop.Enabled = false;
        TopMost = false;
        UpdateIdleStatus();
    }

    private void ExpireAlarm(DateTimeOffset now)
    {
        if (alarming && now >= alarmEndsAt) StopAlarm();
    }

    private void SetAlarmEnabled(bool enabled, bool save = true)
    {
        alarmEnabled = enabled;
        monitoringVersion++;
        tracker.Reset();
        alarmOn.Checked = enabled;
        alarmOff.Checked = !enabled;
        test.Enabled = enabled;
        StopAlarm();
        if (save) SaveSettings();
    }

    private void UpdateIdleStatus() => status.Text = !alarmEnabled
        ? "Alarm is off. Select On to listen for new notifications."
        : poll.Enabled ? "Listening for new Outlook notifications…" : "Alarm stopped. Enable notification access to listen.";

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
            selectedSoundPath = path;
            SaveSettings();
            if (alarming) sound.Play();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Cannot set sound"); }
    }

    private string? selectedSoundPath;
    private bool loadingSettings;

    private void SaveSettings()
    {
        if (loadingSettings) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(new Settings(selectedSoundPath, alarmEnabled, (duration.SelectedIndex + 1) * 5)));
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Cannot save settings"); }
    }

    private void LoadSettings()
    {
        soundLabel.Text = "Sound: Windows notification sound";
        loadingSettings = true;
        try
        {
            if (!File.Exists(settingsPath)) return;
            var json = File.ReadAllText(settingsPath);
            using var document = JsonDocument.Parse(json);
            var settings = document.RootElement.ValueKind == JsonValueKind.Object
                ? JsonSerializer.Deserialize<Settings>(json)!
                : new Settings(JsonSerializer.Deserialize<string?>(json));
            alarmEnabled = settings.AlarmEnabled;
            duration.SelectedIndex = settings.DurationMinutes switch { 10 => 1, 15 => 2, _ => 0 };
            var path = settings.SoundPath;
            sound.SetFile(path);
            selectedSoundPath = path;
            if (path is not null) soundLabel.Text = "Sound: " + Path.GetFileName(path);
        }
        catch { status.Text = "Saved sound unavailable; using the Windows notification sound."; }
        finally { loadingSettings = false; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { poll.Dispose(); repeat.Dispose(); alarmTimeout.Dispose(); tray.Dispose(); tray.ContextMenuStrip?.Dispose(); sound.Dispose(); }
        base.Dispose(disposing);
    }
}
