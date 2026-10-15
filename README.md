# Relay Alarm

A C# / .NET 10 Windows Forms app for **New Outlook for Windows**. A new Outlook Windows notification starts a repeating sound and brings up a large **Stop** button. Stop silences the sound immediately; the next new notification starts it again. Closing the window keeps the app running in the system tray. Use the tray menu to exit.

## Share with friends

Run `powershell -ExecutionPolicy Bypass -File .\Build-Installer.ps1` to create **`installer\RelayAlarm-Setup.exe`**. Share that executable and `installer\INSTALL.txt`. A SHA-256 checksum is included in `installer\SHA256SUMS.txt`.

The setup includes the app and .NET runtime, installs for the current user under `%LOCALAPPDATA%\Programs\RelayAlarm`, registers the Start entry, and adds an uninstall entry in Windows Settings > Apps. Your friends need x64 Windows 10 version 2004 or newer. Setup requires accepting a **Developer Mode agreement** before Install becomes available. If Developer Mode is off, Setup requests Windows administrator approval to enable it; canceling stops installation. Only the setting change runs elevated, so the app is installed for the original user even when another administrator supplies credentials. Then they open Relay Alarm from Start and allow notification access. Developer Mode is system-wide, permits development app registration including unsigned packages, and stays enabled after a failed install or uninstall; users can turn it off in Windows Developer settings. Setup does not enable Device Portal or Device Discovery, override a policy blocking Developer Mode, or install certificates. It is unsigned, so Windows may warn about the publisher or block it. A distribution without Developer Mode requires a signed Windows package.

Exit Relay Alarm from the tray before installing an update or uninstalling. Re-running Setup registers a fresh app folder; older app folders are kept until uninstall. Uninstall keeps sound preferences. The build uses a fresh staging folder to exclude local diagnostics and other development leftovers.

## Build and install for development

Requires Windows 10 version 2004 or newer (Windows 11 recommended), x64, and the .NET 10 SDK to build.

```powershell
.\Build.ps1
.\Install.ps1
```

This is a local development package. Enable **Developer Mode** in Windows Settings before installing. The installer registers the published folder as a Windows app so the notification listener has a package identity. It does not change Windows security settings or install a certificate. Keep the `dist\RelayAlarm` folder in place after registration. Open **Relay Alarm** from Start and approve Windows' notification access prompt. If PowerShell blocks scripts, use `powershell -ExecutionPolicy Bypass -File .\Build.ps1` (and likewise for Install.ps1).

The default build includes the .NET runtime. `Build.ps1 -FrameworkDependent` makes a smaller build that requires the .NET 10 Desktop Runtime on the target computer. The published folder includes install/uninstall scripts and can be copied to another x64 Windows computer before registration.

After installation, use Start or `powershell -ExecutionPolicy Bypass -File .\Launch.ps1` to launch with Windows app identity. Opening the executable directly also attempts to relaunch the registered app. If connection fails, the full error is saved to `%LOCALAPPDATA%\RelayAlarm\connection-error.txt`. `Diagnose.ps1` checks the registered app's identity, permission, and ability to read notifications without recording notification content or requesting permission.

## Use

1. Enable notifications in New Outlook and Windows Settings. Outlook must produce an actual Windows notification for the app to detect it.
2. Leave Relay Alarm running. It checks Windows notification history every second and ignores notifications already present when it connects.
3. When a new Outlook notification arrives, the sound repeats every 2.5 seconds until you press **Stop** (also available in the tray menu).
4. Use **Test sound** to try the repeating alert. **Choose WAV…** selects a WAV file; **Windows sound** restores the configured Windows notification sound. The sound choice is saved locally.

Windows does not expose another app's original notification audio through this listener. The app plays the Windows notification sound or your selected WAV. Long WAV files restart every 2.5 seconds; short notification sounds work best. A muted Windows sound scheme or muted output will also mute the default alert.

The listener follows Outlook notifications, including any calendar alerts emitted by the same app. It does not log into your mailbox, read message bodies, remove notifications, or store their content. Windows grants the listener access to notifications from all apps, but Relay Alarm filters by New Outlook's application identity. Notifications suppressed by Outlook, Windows, or organization policy cannot trigger an alarm. Notifications removed before the next poll can be missed. It does not automatically start at sign-in; open it when needed. Exiting the application or shutting down Windows ends the alert.

## Verify

```powershell
dotnet build -c Release
dotnet run --project .\tests\RelayAlarm.Checks.csproj
.\dist\RelayAlarm\RelayAlarm.exe --smoke-test
```

The smoke test creates a real window, verifies alarm/Stop state and timer shutdown without playing sound or requesting access, and writes `smoke-test.txt` beside the executable. To verify end to end, install, allow access, send yourself an email while Outlook is minimized, check the sound repeats, press Stop, then send a second email.

Unregister with `Uninstall.ps1`. Preferences remain in `%LOCALAPPDATA%\RelayAlarm\settings.json`.

Implementation references: [Windows notification listener](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/notification-listener).
