using QuestBell.Core;
using System.Text.Json;

int passed = 0;
void Check(string name, bool condition) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS " + name); passed++; }
Reminder Task(DateTime date, string repeat = "monthly") => new() { Title = "Pay phone bill", AnchorAt = date, OccurrenceAt = date, NextAt = date, Repeat = repeat };
var jan31 = Task(new DateTime(2026, 1, 31, 18, 0, 0));
Check("Jan31 clamps to Feb28", Schedule.Next(jan31, new DateTime(2026, 1, 31, 19, 0, 0)) == new DateTime(2026, 2, 28, 18, 0, 0));
jan31.OccurrenceAt = new DateTime(2026, 2, 28, 18, 0, 0);
Check("Monthly anchor restores Mar31", Schedule.Next(jan31, new DateTime(2026, 2, 28, 19, 0, 0)) == new DateTime(2026, 3, 31, 18, 0, 0));
Check("Missed months produce one next future occurrence", Schedule.Next(jan31, new DateTime(2026, 7, 1)) == new DateTime(2026, 7, 31, 18, 0, 0));
var leap = Task(new DateTime(2024, 2, 29, 9, 0, 0), "yearly");
Check("Leap-year clamps", Schedule.Next(leap, leap.NextAt) == new DateTime(2025, 2, 28, 9, 0, 0));
leap.OccurrenceAt = new DateTime(2027, 2, 28, 9, 0, 0);
Check("Leap-year anchor restores Feb29", Schedule.Next(leap, leap.OccurrenceAt) == new DateTime(2028, 2, 29, 9, 0, 0));
var interval = Task(new DateTime(2026, 1, 1, 10, 0, 0), "weekly"); interval.Interval = 2;
Check("Fortnight recurrence", Schedule.Next(interval, interval.NextAt) == interval.AnchorAt.AddDays(14));
var after = Task(new DateTime(2026, 1, 1, 10, 0, 0), "monthly"); after.AfterCompletion = true;
Check("Completion-relative recurrence", Schedule.Next(after, new DateTime(2026, 1, 20, 13, 0, 0)) == new DateTime(2026, 2, 20, 13, 0, 0));
after.OccurrenceAt = new DateTime(2026, 6, 1, 10, 0, 0);
Check("Early completion restarts completion-relative interval", Schedule.Next(after, new DateTime(2026, 1, 20, 13, 0, 0)) == new DateTime(2026, 2, 20, 13, 0, 0));
var weekdays = Task(new DateTime(2026, 10, 16, 9, 0, 0), "weekdays"); weekdays.Weekdays = [DayOfWeek.Monday, DayOfWeek.Wednesday];
Check("Weekdays skip weekend", Schedule.Next(weekdays, weekdays.NextAt) == new DateTime(2026, 10, 19, 9, 0, 0));
Check("One-off ends", Schedule.Next(Task(DateTime.Today, "none"), DateTime.Now) is null);
Check("Calendar-month relative delay", Schedule.Add(new DateTime(2026, 1, 31), 1, "months") == new DateTime(2026, 2, 28));
var data = new AppData(); var r = Task(new DateTime(2026, 1, 31, 18, 0, 0)); r.DueAt = new DateTime(2026, 2, 2, 23, 59, 0); data.Reminders.Add(r);
var history = data.Resolve(r, "done", new DateTime(2026, 2, 1));
Check("Completion advances occurrence", r.NextAt == new DateTime(2026, 2, 28, 18, 0, 0));
Check("Due offset preserved", r.DueAt == new DateTime(2026, 3, 2, 23, 59, 0));
Check("History increments collection", data.CompletedCount == 1);
Check("Undo completion", data.Undo(history) && !data.Reminders[0].Finished && data.CompletedCount == 0);
Check("Double undo blocked", !data.Undo(history));
r = data.Reminders[0]; history = data.Resolve(r, "skip", new DateTime(2026, 2, 1));
Check("Skip is not completion", data.CompletedCount == 0 && !r.Finished);
r.Revision++;
Check("Undo cannot overwrite later edit", !data.Undo(history));
var once = Task(DateTime.Today, "none"); data.Reminders.Add(once); data.Resolve(once, "done", DateTime.Now);
Check("One-off completion finishes", once.Finished);
var snoozed = Task(new DateTime(2026, 1, 31)); snoozed.NextAt = new DateTime(2026, 3, 4); snoozed.Snoozed = true;
Check("Snooze preserves original schedule", Schedule.Next(snoozed, snoozed.NextAt) == new DateTime(2026, 3, 31));
Check("Safe https accepted", Schedule.SafeUrl("https://example.com/pay", out _));
Check("Bare website normalized", Schedule.SafeUrl("example.com", out var safe) && safe!.Scheme == "https");
Check("File links rejected", !Schedule.SafeUrl("file:///C:/Windows/notepad.exe", out _));
Check("Script links rejected", !Schedule.SafeUrl("javascript://alert(1)", out _));
Check("Embedded credentials rejected", !Schedule.SafeUrl("https://name:password@example.com", out _));
Check("English puzzle", Catalog.Title("pay", "phone", "bill", "en") == "Pay phone bill");
Check("Greek puzzle grammar", Catalog.Title("pay", "phone", "bill", "el") == "Πλήρωσε λογαριασμό τηλεφώνου");
Check("Custom wording preserved", Catalog.Title("=Do", "=special", "=thing", "en") == "Do special thing");
Check("Automatic custom art match", Catalog.MatchCategory("", "", "", "Cancel service") == "subscriptions");
Check("Greek custom art match", Catalog.MatchCategory("", "", "", "Πλήρωσε ενοίκιο") == "bills");
Check("Delay scales through 12 months", Catalog.Delays.Last() == new Delay(12, "months"));
Check("Artwork library contains 100 unique sprites", Artwork.Total == 100 && Artwork.All.Select(a => (a.Sheet, a.Cell)).Distinct().Count() == 100);
Check("Original pinned artwork and collection order preserved", Catalog.CategoryIds.SelectMany(c => Enumerable.Range(0, 4).Select(v => (c, v))).Select((x, i) => Artwork.Get(x.c, x.v) == Artwork.All[i] && Artwork.Get(x.c, x.v).Sheet == x.c && Artwork.Get(x.c, x.v).Cell == x.v).All(x => x));
foreach (var template in Catalog.Templates)
{
    var taskArt = new Reminder { Title = Catalog.Title(template, "en"), ActionId = template.Action, SubjectId = template.Subject, ObjectId = template.Object, Category = template.Category };
    Check("Relevant new artwork for " + template.Id, Artwork.Candidates(taskArt).All(v => v >= 4) && Artwork.Candidates(taskArt).Length >= 2);
}
var phoneArt = new Reminder { Title = "Pay phone bill", Category = "bills", NextAt = DateTime.Today, AnchorAt = DateTime.Today, OccurrenceAt = DateTime.Today };
Check("Phone thumbnail starts with a phone", Artwork.Get(phoneArt.Category, Artwork.DisplayVariant(phoneArt)).Sheet == "phone-friends");
phoneArt.LastArtVariant = Artwork.Preferred(phoneArt);
Check("Automatic artwork never immediately repeats", Enumerable.Range(0, 100).All(_ => Artwork.NextVariant(phoneArt) != phoneArt.LastArtVariant));
phoneArt.ArtVariant = 1; phoneArt.PinArt = true;
Check("Legacy pinned art takes priority over matching", Artwork.NextVariant(phoneArt) == 1 && Artwork.DisplayVariant(phoneArt) == 1);
phoneArt.ArtVariant = 5;
Check("New pinned art takes priority over matching", Artwork.NextVariant(phoneArt) == 5);
Check("English custom dentist matches beaver and tooth", Artwork.Candidates(new Reminder { Title = "Book dentist", CustomTitle = true, Category = "appointments" }).All(v => Artwork.Get("appointments", v).Topics == "dentist"));
Check("Accented Greek custom phone matches phone art", Artwork.Topic(new Reminder { Title = "Πλήρωσε λογαριασμό τηλεφώνου", CustomTitle = true }) == "phone");
Check("Greek custom dentist works across families", Artwork.Candidates(new Reminder { Title = "Επίσκεψη στον οδοντίατρο", CustomTitle = true, Category = "personal" }).All(v => Artwork.Get("personal", v).Topics == "dentist"));
Check("English card does not falsely match car", Artwork.Topic(new Reminder { Title = "Send card", CustomTitle = true }) == "");
Check("Unknown custom task uses family fallback", Artwork.Candidates(new Reminder { Title = "Do unusual task", CustomTitle = true, Category = "personal" }).SequenceEqual(new[] { 0, 1, 2, 3 }));
var artBackup = new AppData { Reminders = [phoneArt.Clone()] }; artBackup.Validate();
var artRoundTrip = System.Text.Json.JsonSerializer.Deserialize<AppData>(System.Text.Json.JsonSerializer.Serialize(artBackup, Store.Json), Store.Json)!; artRoundTrip.Validate();
Check("New pinned art survives backup round trip", artRoundTrip.Reminders[0].ArtVariant == 5);
bool badArtRejected = false; artRoundTrip.Reminders[0].ArtVariant = 76;
try { artRoundTrip.Validate(); } catch (InvalidDataException) { badArtRejected = true; }
Check("Unknown artwork ID rejected in backup", badArtRejected);
var storagePath = Path.Combine(Path.GetTempPath(), "QuestBell-tests-" + Guid.NewGuid().ToString("N"));
var store = new Store(storagePath); store.Save(data); store.Save(data);
var loaded = store.Load(); Check("Persistence round trip", loaded.Reminders.Count == 2 && loaded.History.Count == 3);
loaded.Reminders[0].Snoozed = true; loaded.Reminders[0].NextAt = DateTime.Today.AddMonths(1); store.Save(loaded);
Check("Snooze survives reload", store.Load().Reminders[0].NextAt == DateTime.Today.AddMonths(1) && store.Load().Reminders[0].Snoozed);
File.WriteAllText(store.FilePath, "{ broken json");
Check("Damaged JSON recovers previous valid backup", store.Load().Reminders.Count == 2 && store.Recovered);
Check("Damaged file preserved", Directory.GetFiles(storagePath, "*.damaged-*").Length == 1);
var invalid = new AppData(); invalid.SchemaVersion = 99;
bool rejected = false; try { invalid.Validate(); } catch (InvalidDataException) { rejected = true; }
Check("Unknown backup version rejected", rejected);
var duplicate = new AppData(); duplicate.Reminders = [Task(DateTime.Today), Task(DateTime.Today)]; duplicate.Reminders[1].Id = duplicate.Reminders[0].Id;
rejected = false; try { duplicate.Validate(); } catch (InvalidDataException) { rejected = true; }
Check("Duplicate reminder IDs rejected", rejected);
var nullRecord = new AppData(); nullRecord.Reminders.Add(null!);
rejected = false; try { nullRecord.Validate(); } catch (InvalidDataException) { rejected = true; }
Check("Null backup record rejected safely", rejected);
var athens = TimeZoneInfo.FindSystemTimeZoneById("GTB Standard Time");
Check("DST spring gap normalized", !athens.IsInvalidTime(Schedule.Wall(new DateTime(2026, 3, 29, 3, 30, 0), athens)));
CloudTests.Run(Check);
UpdateTests.Run(Check);
QuickTests.Run(Check);
Check("26 unique work templates", WorkCatalog.Templates.Length == 26 && WorkCatalog.Templates.Select(t => t.Id).Distinct().Count() == 26);
foreach (var work in WorkCatalog.Templates)
{
    var task = new Reminder { IsWork = true, WorkTemplateId = work.Id, WorkReference = "XYZ-42", Title = WorkCatalog.Title(work, "XYZ-42", "en"), Category = work.Category, Repeat = work.Repeat, NextAt = DateTime.Today, AnchorAt = DateTime.Today, OccurrenceAt = DateTime.Today };
    new AppData { Reminders = [task] }.Validate();
    Check("Work template bilingual title and related art: " + work.Id, Catalog.Title(task, "el").Contains("XYZ-42") && Catalog.Title(task, "el") != Catalog.Title(task, "en") && Artwork.Topic(task) == work.Topic && Artwork.Candidates(task).Length == 4 && Artwork.Candidates(task).All(v => v >= 52));
}
Check("Old personal artwork IDs remain stable", Artwork.Get("personal", 51).En == "Heart keeper cat" && Artwork.Get("bills", 4).En == "Pocket phone & bill");
foreach (var (text, topic) in new[] { ("Email the client", "email"), ("Ship order for client", "shipping"), ("Write blog post", "blog"), ("Weekly newsletter", "newsletter"), ("Social media post", "social"), ("Order supplies", "office"), ("Στείλε την παραγγελία", "shipping"), ("Στείλε email στον πελάτη", "email"), ("Γράψε άρθρο", "blog"), ("Ανάρτηση στα κοινωνικά δίκτυα", "social") })
    Check("Custom work artwork: " + text, Artwork.Topic(new Reminder { IsWork = true, CustomTitle = true, Title = text }) == "work-" + topic);
Check("Weekly newsletter defaults to weekly", WorkCatalog.Find("weekly-newsletter")!.Repeat == "weekly");
Console.WriteLine($"\n{passed} checks passed.");
