namespace QuestBell.Core;

public sealed class QuickReminder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public bool Work { get; set; }
    public string Category { get; set; } = "personal";
    public string Repeat { get; set; } = "none";
}

public sealed class QuickTime
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "clock";
    public int Value { get; set; }
    public string Unit { get; set; } = "minutes";
}

public static class QuickOptions
{
    public static List<QuickTime> DefaultTimes() => new[] { 0, 1, 7 }.Select(n => new QuickTime { Kind = "date", Value = n, Unit = "days" })
        .Concat(new[] { 9, 12, 17, 21 }.Select(h => new QuickTime { Value = h * 60 })).ToList();
    internal static void UpgradeDefaultTimes(Preferences preferences)
    {
        // Only upgrade the complete previous default set; preserve customized shortcuts and IDs.
        var times = preferences.QuickTimes;
        var previous = new[] { 0, 1, 7, 9 * 60, 12 * 60, 18 * 60, 20 * 60 };
        if (times.Count != previous.Length || times.Where((t, i) => t.Kind != (i < 3 ? "date" : "clock") || t.Value != previous[i]).Any()) return;
        times[5].Value = 17 * 60; times[6].Value = 21 * 60;
    }
    public static void Validate(Preferences preferences)
    {
        var reminders = preferences.QuickReminders; var times = preferences.QuickTimes;
        if (reminders is null || times is null || reminders.Count > 100 || times.Count > 30 || reminders.Any(r => r is null) || times.Any(t => t is null)) throw new InvalidDataException("Invalid quick choices.");
        if (reminders.Any(r => string.IsNullOrWhiteSpace(r.Id) || string.IsNullOrWhiteSpace(r.Title) || r.Title.Length > 500 || !Catalog.CategoryIds.Contains(r.Category) || !Schedule.Repeats.Contains(r.Repeat) || r.Repeat == "weekdays") || reminders.Select(r => r.Id).Distinct().Count() != reminders.Count) throw new InvalidDataException("Invalid quick reminder.");
        if (times.Any(t => string.IsNullOrWhiteSpace(t.Id) || (t.Kind switch { "clock" => t.Value is < 0 or > 1439, "date" => t.Value is < 0 or > 365, "delay" => t.Value is < 1 or > 1200 || t.Unit is not ("minutes" or "hours" or "days" or "weeks" or "months"), _ => true })) || times.Select(t => t.Id).Distinct().Count() != times.Count) throw new InvalidDataException("Invalid quick time button.");
    }
    public static DateTime Apply(QuickTime time, DateTime current, DateTime now) => time.Kind switch
    {
        "clock" => current.Date.AddMinutes(time.Value),
        "date" => now.Date.AddDays(time.Value).Add(current.TimeOfDay),
        "delay" => Schedule.Add(now, time.Value, time.Unit),
        _ => throw new InvalidDataException("Invalid quick time button.")
    };
}
