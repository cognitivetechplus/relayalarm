using RelayAlarm;

var tracker = new NotificationTracker();
void Check(bool result, string description)
{
    if (!result) throw new Exception(description);
    Console.WriteLine("PASS: " + description);
}
Check(!tracker.Update([("old-mail", true)]), "Startup notifications do not trigger");
Check(!tracker.Update([("old-mail", true), ("other-app", false)]), "Other applications do not trigger");
Check(tracker.Update([("old-mail", true), ("new-mail", true)]), "New Outlook notification triggers");
Check(!tracker.Update([("old-mail", true), ("new-mail", true)]), "Stopped notification does not trigger again");
Check(!tracker.Update([]), "Dismissing notifications does not trigger");
Check(tracker.Update([("another-mail", true)]), "Next mail triggers after dismissal");
