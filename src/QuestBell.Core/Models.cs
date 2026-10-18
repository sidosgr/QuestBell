using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuestBell.Core;

public sealed class Reminder
{
    public bool IsWork { get; set; }
    public string WorkTemplateId { get; set; } = "";
    public string WorkReference { get; set; } = "";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string ActionId { get; set; } = "pay";
    public string SubjectId { get; set; } = "phone";
    public string ObjectId { get; set; } = "bill";
    public bool CustomTitle { get; set; }
    public string Category { get; set; } = "bills";
    public DateTime AnchorAt { get; set; }
    public DateTime OccurrenceAt { get; set; }
    public DateTime NextAt { get; set; }
    public DateTime? DueAt { get; set; }
    public string Repeat { get; set; } = "none";
    public int Interval { get; set; } = 1;
    public List<DayOfWeek> Weekdays { get; set; } = [];
    public bool AfterCompletion { get; set; }
    public bool Enabled { get; set; } = true;
    public bool Finished { get; set; }
    public string Amount { get; set; } = "";
    public string Url { get; set; } = "";
    public string Note { get; set; } = "";
    public bool Sound { get; set; } = true;
    public int FollowUpMinutes { get; set; }
    public DateTime? FollowUpAt { get; set; }
    public int ArtVariant { get; set; } = -1;
    public int LastArtVariant { get; set; } = -1;
    public bool PinArt { get; set; }
    public int Revision { get; set; }
    public bool Snoozed { get; set; }
    [JsonIgnore] public bool Active => Enabled && !Finished;
    public Reminder Clone() => JsonSerializer.Deserialize<Reminder>(JsonSerializer.Serialize(this))!;
}

public sealed class HistoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public Reminder Before { get; set; } = new();
    public string Action { get; set; } = "done";
    public DateTime At { get; set; }
    public int ResultRevision { get; set; }
    public bool Undone { get; set; }
}

public sealed class Preferences
{
    public List<QuickReminder> QuickReminders { get; set; } = [];
    public List<QuickTime> QuickTimes { get; set; } = QuickOptions.DefaultTimes();
    public bool CheckUpdates { get; set; } = true;
    public bool WorkMode { get; set; }
    public string Language { get; set; } = "en";
    public bool AutoStart { get; set; } = true;
    public bool Mute { get; set; }
    public bool ReduceMotion { get; set; }
    public bool Collection { get; set; } = true;
    public string Monitor { get; set; } = "primary";
    public int SnoozeMinutes { get; set; } = 10;
    public int LinkReturnMinutes { get; set; } = 10;
    public DateTime? PausedUntil { get; set; }
}

public sealed class AppData
{
    // Merge bookkeeping is committed atomically with the local reminders.
    public SyncDocument? SyncState { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public Preferences Settings { get; set; } = new();
    public List<Reminder> Reminders { get; set; } = [];
    public List<HistoryEntry> History { get; set; } = [];
    public Dictionary<string, List<string>> CustomPhrases { get; set; } = new()
    { ["action"] = [], ["subject"] = [], ["object"] = [] };
    public List<string> RecentTemplates { get; set; } = [];
    [JsonIgnore] public int CompletedCount => History.Count(x => x.Action == "done" && !x.Undone);

    public HistoryEntry Resolve(Reminder task, string action, DateTime now)
    {
        var entry = new HistoryEntry { Before = task.Clone(), Action = action, At = now };
        var next = Schedule.Next(task, now);
        task.Finished = next is null;
        if (next.HasValue)
        {
            if (task.DueAt.HasValue)
                task.DueAt = next.Value + (task.DueAt.Value - task.OccurrenceAt);
            task.NextAt = task.OccurrenceAt = next.Value;
        }
        task.Snoozed = false;
        task.FollowUpAt = null;
        task.Revision++;
        entry.ResultRevision = task.Revision;
        History.Insert(0, entry);
        return entry;
    }

    public bool Undo(HistoryEntry entry)
    {
        var task = Reminders.FirstOrDefault(x => x.Id == entry.Before.Id);
        if (entry.Undone || task is null || task.Revision != entry.ResultRevision) return false;
        if (SyncState?.Entries.TryGetValue("h:" + entry.Id, out var historyStamp) == true &&
            SyncState.Entries.TryGetValue("r:" + task.Id, out var taskStamp) &&
            (historyStamp.Counter != taskStamp.Counter || historyStamp.Device != taskStamp.Device)) return false;
        var restored = entry.Before.Clone();
        restored.Revision = task.Revision + 1;
        Reminders[Reminders.IndexOf(task)] = restored;
        entry.Undone = true;
        return true;
    }

    public void Validate()
    {
        if (Settings is not null) QuickOptions.Validate(Settings);
        if (SchemaVersion != 1) throw new InvalidDataException("Unsupported backup version.");
        if (Reminders is null || Settings is null || History is null || CustomPhrases is null || RecentTemplates is null)
            throw new InvalidDataException("Incomplete backup.");
        if (Reminders.Count > 20000 || History.Count > 200000)
            throw new InvalidDataException("Backup exceeds supported size.");
        if (Reminders.Any(x => x is null) || History.Any(x => x is null || x.Before is null))
            throw new InvalidDataException("Invalid empty reminder or history record.");
        if (Reminders.Select(x => x.Id).Distinct().Count() != Reminders.Count)
            throw new InvalidDataException("Duplicate reminder identifiers.");
        if (Settings.Language is not ("en" or "el") || Settings.SnoozeMinutes is < 1 or > 525600 || Settings.LinkReturnMinutes is < 1 or > 525600)
            throw new InvalidDataException("Invalid settings.");
        if (History.Any(x => x.Action is not ("done" or "skip"))) throw new InvalidDataException("Invalid history action.");
        foreach (var r in Reminders.Concat(History.Select(x => x.Before)))
        {
            if (r.WorkReference is null || r.WorkReference.Length > 200 || r.WorkTemplateId is null || (r.WorkTemplateId.Length > 0 && WorkCatalog.Find(r.WorkTemplateId) is null))
                throw new InvalidDataException("Invalid work reminder.");
            if (string.IsNullOrWhiteSpace(r.Id) || string.IsNullOrWhiteSpace(r.Title) || r.Title.Length > 500 ||
                string.IsNullOrWhiteSpace(r.ActionId) || string.IsNullOrWhiteSpace(r.SubjectId) || string.IsNullOrWhiteSpace(r.ObjectId) ||
                r.Note is null || r.Note.Length > 10000 || r.Url is null || r.Amount is null || r.Interval < 1 || r.Interval > 365 ||
                r.NextAt.Year < 2000 || r.NextAt.Year > 2200 || r.AnchorAt.Year < 2000 || r.AnchorAt.Year > 2200 ||
                r.OccurrenceAt.Year < 2000 || r.OccurrenceAt.Year > 2200 || (r.DueAt.HasValue && (r.DueAt.Value.Year < 2000 || r.DueAt.Value.Year > 2200)) || r.Weekdays is null ||
                !Catalog.CategoryIds.Contains(r.Category) || !Schedule.Repeats.Contains(r.Repeat) || r.FollowUpMinutes < 0 ||
                !Artwork.ValidVariant(r.ArtVariant) || !Artwork.ValidVariant(r.LastArtVariant) ||
                (r.Repeat == "weekdays" && (r.Weekdays.Count == 0 || r.Weekdays.Any(d => (int)d < 0 || (int)d > 6))))
                throw new InvalidDataException("Invalid reminder in backup.");
            if (r.Url.Length > 0 && !Schedule.SafeUrl(r.Url, out _)) throw new InvalidDataException("Unsafe website address.");
        }
        foreach (var key in new[] { "action", "subject", "object" })
            if (!CustomPhrases.ContainsKey(key) || CustomPhrases[key] is null || CustomPhrases[key].Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 100))
                throw new InvalidDataException("Invalid custom phrases.");
    }
}
