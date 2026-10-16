namespace RelayAlarm;

// IDs can be reused; creation time distinguishes a later notification with the same ID.
internal sealed class NotificationTracker
{
    private HashSet<string> previous = [];
    private bool initialized;

    public void Reset()
    {
        previous.Clear();
        initialized = false;
    }

    public bool Update(IEnumerable<(string Key, bool IsOutlook)> notifications)
    {
        var current = notifications.ToArray();
        var trigger = initialized && current.Any(n => n.IsOutlook && !previous.Contains(n.Key));
        previous = current.Select(n => n.Key).ToHashSet();
        initialized = true;
        return trigger;
    }
}
