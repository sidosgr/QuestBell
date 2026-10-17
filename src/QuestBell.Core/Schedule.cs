namespace QuestBell.Core;

public static class Schedule
{
    public static readonly string[] Repeats = ["none", "daily", "weekly", "monthly", "yearly", "weekdays"];

    public static DateTime Wall(DateTime time, TimeZoneInfo? zone = null)
    {
        time = DateTime.SpecifyKind(time, DateTimeKind.Unspecified);
        zone ??= TimeZoneInfo.Local;
        // Nonexistent spring-forward times move to the first valid minute.
        while (zone.IsInvalidTime(time)) time = time.AddMinutes(1);
        return time;
    }

    public static DateTime? Next(Reminder r, DateTime now)
    {
        if (r.Repeat == "none") return null;
        int interval = Math.Clamp(r.Interval, 1, 365);
        DateTime anchor = r.AfterCompletion ? now : r.AnchorAt;
        DateTime threshold = r.AfterCompletion || now > r.OccurrenceAt ? now : r.OccurrenceAt;
        DateTime candidate;
        if (r.Repeat == "weekdays")
        {
            for (var date = threshold.Date; date <= threshold.Date.AddDays(8); date = date.AddDays(1))
            {
                candidate = Wall(date + anchor.TimeOfDay);
                if (candidate > threshold && r.Weekdays.Contains(date.DayOfWeek)) return candidate;
            }
            throw new InvalidOperationException("Select at least one weekday.");
        }
        for (int step = 1; step <= 80000; step++)
        {
            try
            {
                candidate = r.Repeat switch
                {
                    "daily" => anchor.AddDays((long)interval * step),
                    "weekly" => anchor.AddDays(7L * interval * step),
                    "monthly" => anchor.AddMonths(interval * step),
                    "yearly" => anchor.AddYears(interval * step),
                    _ => throw new InvalidOperationException("Unknown recurrence.")
                };
            }
            catch (ArgumentOutOfRangeException) { return null; }
            candidate = Wall(candidate);
            if (candidate > threshold) return candidate;
        }
        throw new InvalidOperationException("Unable to calculate recurrence.");
    }

    public static DateTime Add(DateTime from, int value, string unit) => Wall(unit switch
    {
        "minutes" => from.AddMinutes(value), "hours" => from.AddHours(value),
        "days" => from.AddDays(value), "weeks" => from.AddDays(7L * value),
        "months" => from.AddMonths(value), _ => throw new ArgumentException("Unknown time unit.")
    });

    public static bool SafeUrl(string text, out Uri? uri)
    {
        if (!text.Contains("://")) text = "https://" + text;
        return Uri.TryCreate(text, UriKind.Absolute, out uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
            !string.IsNullOrWhiteSpace(uri.Host) && string.IsNullOrEmpty(uri.UserInfo);
    }

    public static string Group(Reminder r, DateTime now) =>
        (r.DueAt.HasValue && r.DueAt.Value.Date < now.Date) || r.NextAt.Date < now.Date
            ? "overdue" : r.NextAt.Date == now.Date ? "today" : "upcoming";
}
