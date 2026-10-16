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
tracker.Reset();
Check(!tracker.Update([("arrived-while-disabled", true)]), "Re-enabling ignores notifications received while disabled");
Check(tracker.Update([("arrived-while-disabled", true), ("after-enabling", true)]), "New notifications trigger after re-enabling");
