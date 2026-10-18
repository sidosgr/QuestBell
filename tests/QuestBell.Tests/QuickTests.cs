using QuestBell.Core;
using System.Text.Json;

internal static class QuickTests
{
    internal static void Run(Action<string, bool> check)
    {
        var legacy = JsonSerializer.Deserialize<AppData>("{}")!;
        check("Legacy data receives default quick buttons", legacy.Settings.QuickTimes.Count == 7 && legacy.Settings.QuickReminders.Count == 0);
        var a = new Preferences(); var b = new Preferences(); a.QuickTimes.Clear();
        check("Quick button lists are independent per preferences instance", b.QuickTimes.Count == 7);
        var now = new DateTime(2026, 1, 31, 14, 25, 0); var selected = new DateTime(2026, 2, 10, 8, 45, 0);
        check("Clock button preserves selected date", QuickOptions.Apply(new QuickTime { Value = 9 * 60 + 30 }, selected, now) == new DateTime(2026, 2, 10, 9, 30, 0));
        check("Date button preserves entered time", QuickOptions.Apply(new QuickTime { Kind = "date", Value = 1 }, selected, now) == new DateTime(2026, 2, 1, 8, 45, 0));
        check("Relative button uses current time", QuickOptions.Apply(new QuickTime { Kind = "delay", Value = 15, Unit = "minutes" }, selected, now) == now.AddMinutes(15));
        check("Month quick delay uses calendar months", QuickOptions.Apply(new QuickTime { Kind = "delay", Value = 1, Unit = "months" }, selected, now) == new DateTime(2026, 2, 28, 14, 25, 0));
        var data = new AppData(); data.Settings.QuickTimes.Clear(); data.Validate();
        check("Removing all quick buttons is supported", data.Settings.QuickTimes.Count == 0);
        data.Settings.QuickReminders.Add(new QuickReminder { Title = "Έλεγξε τις παραγγελίες", Work = true, Category = "shopping", Repeat = "weekly" });
        var copy = CloudSync.Backup(data); copy.Validate();
        check("Quick choices survive backup round trip", copy.Settings.QuickReminders.Single().Title == "Έλεγξε τις παραγγελίες" && copy.Settings.QuickReminders.Single().Work && copy.Settings.QuickReminders.Single().Repeat == "weekly" && copy.Settings.QuickTimes.Count == 0);
        var folder = Path.Combine(Path.GetTempPath(), "QuestBell-quick-test-" + Guid.NewGuid().ToString("N")); var store = new Store(folder); store.Save(data);
        check("Quick choices survive disk reload", store.Load().Settings.QuickReminders.Count == 1 && store.Load().Settings.QuickTimes.Count == 0);
        check("Custom project email gets email artwork", Artwork.Topic(new Reminder { IsWork = true, CustomTitle = true, Title = "Email project team" }) == "work-email");
        bool Reject(Action action) { try { action(); return false; } catch (InvalidDataException) { return true; } }
        data.Settings.QuickTimes.Add(new QuickTime { Value = 1440 });
        check("Invalid clock button rejected", Reject(data.Validate)); data.Settings.QuickTimes.Clear();
        data.Settings.QuickTimes.Add(new QuickTime { Kind = "delay", Value = 0, Unit = "hours" });
        check("Zero delay rejected", Reject(data.Validate)); data.Settings.QuickTimes.Clear();
        data.Settings.QuickTimes.Add(new QuickTime { Kind = "delay", Value = 2, Unit = "years" });
        check("Unknown delay unit rejected", Reject(data.Validate)); data.Settings.QuickTimes.Clear();
        data.Settings.QuickReminders[0].Title = "";
        check("Empty quick reminder rejected", Reject(data.Validate)); data.Settings.QuickReminders[0].Title = "Valid";
        data.Settings.QuickReminders.Add(data.Settings.QuickReminders[0]);
        check("Duplicate quick reminder IDs rejected", Reject(data.Validate));
    }
}
