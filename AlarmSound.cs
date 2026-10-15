using System.Media;
using System.Runtime.InteropServices;

namespace RelayAlarm;

internal sealed partial class AlarmSound : IDisposable
{
    private SoundPlayer? player;
    public void SetFile(string? path)
    {
        SoundPlayer? replacement = null;
        if (!string.IsNullOrEmpty(path))
        {
            replacement = new SoundPlayer(path);
            try { replacement.Load(); }
            catch { replacement.Dispose(); throw; }
        }
        Stop();
        player?.Dispose();
        player = replacement;
    }

    public void Play()
    {
        if (player is not null) player.Play();
        else PlaySound("Notification.Default", IntPtr.Zero, 0x00010000 | 0x0001 | 0x0002);
    }

    public void Stop()
    {
        player?.Stop();
        PlaySound(null, IntPtr.Zero, 0);
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(string? sound, IntPtr module, uint flags);

    public void Dispose() { Stop(); player?.Dispose(); }
}
