using System.Text.Json;
using System.Windows.Threading;

namespace QuestBell;

internal sealed class CloudPreferences
{
    public string Device { get; set; } = Guid.NewGuid().ToString("N");
    public string Folder { get; set; } = "";
    public bool Sync { get; set; }
    public bool Backups { get; set; }
}

internal sealed class CloudService
{
    private readonly App host;
    private readonly string configPath;
    private readonly DispatcherTimer timer;
    private int generation;
    private string lastBackup = "";
    private bool busy;
    internal CloudPreferences Options { get; private set; } = new();
    internal event Action? Changed;
    internal DateTime? LastExchange { get; private set; }
    internal string Error { get; private set; } = "";
    internal bool Busy => busy;
    internal bool Enabled => Options.Sync || Options.Backups;
    internal string Status => busy ? L.T("Checking the shared folder…", "Έλεγχος κοινόχρηστου φακέλου…") : Error.Length > 0
        ? L.T("Needs attention · local reminders still work", "Χρειάζεται έλεγχο · οι τοπικές υπενθυμίσεις λειτουργούν")
        : !Enabled ? L.T("Off · everything stays on this PC", "Ανενεργό · όλα μένουν σε αυτό το PC")
        : LastExchange.HasValue ? L.T("Folder updated · ", "Ενημέρωση φακέλου · ") + LastExchange.Value.ToString("HH:mm:ss")
        : L.T("Waiting for the next folder check", "Αναμονή για τον επόμενο έλεγχο φακέλου");

    internal CloudService(App app)
    {
        host = app; configPath = Path.Combine(app.Storage.DirectoryPath, "cloud-settings.json");
        try
        {
            if (File.Exists(configPath))
            {
                Options = JsonSerializer.Deserialize<CloudPreferences>(File.ReadAllText(configPath)) ?? throw new InvalidDataException();
                if (!Guid.TryParseExact(Options.Device, "N", out _) || Options.Folder is null) throw new InvalidDataException();
            }
        }
        catch { Options = new(); Error = L.T("Cloud settings could not be read. Choose the folder again to reconnect.", "Δεν διαβάστηκαν οι ρυθμίσεις cloud. Διάλεξε ξανά τον φάκελο για σύνδεση."); }
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        timer.Tick += async (_, _) => await Run();
        if (!app.TestMode && Enabled) timer.Start();
    }
    internal void Configure(string folder, bool sync, bool backups)
    {
        if (busy) throw new InvalidOperationException(L.T("Wait for the current folder check to finish.", "Περίμενε να τελειώσει ο τρέχων έλεγχος."));
        if (sync || backups)
        {
            folder = Path.GetFullPath(folder);
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(L.T("Choose an existing, locally available cloud folder.", "Διάλεξε υπάρχοντα φάκελο cloud διαθέσιμο τοπικά."));
            var dataRoot = Path.GetFullPath(host.Storage.DirectoryPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if ((folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar).StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(L.T("Choose a cloud folder separate from the local app data.", "Διάλεξε φάκελο cloud ξεχωριστό από τα τοπικά δεδομένα της εφαρμογής."));
        }
        var options = new CloudPreferences { Device = Options.Device, Folder = folder, Sync = sync, Backups = backups };
        CloudSync.WriteAtomic(configPath, options);
        bool reset = !string.Equals(folder, Options.Folder, StringComparison.OrdinalIgnoreCase);
        Options = options; generation++; lastBackup = ""; Error = ""; LastExchange = null;
        if (reset) { host.Data.SyncState = null; host.Storage.Save(host.Data); }
        timer.Stop(); if (Enabled && !host.TestMode) timer.Start(); Changed?.Invoke();
    }
    internal void Pause()
    {
        // Invalidate any in-flight read before a restore/import changes local data.
        var options = new CloudPreferences { Device = Options.Device, Folder = Options.Folder };
        CloudSync.WriteAtomic(configPath, options); Options = options;
        generation++; timer.Stop(); Error = ""; Changed?.Invoke();
    }
    internal void Stop() { generation++; timer.Stop(); }
    private bool Editing => host.Windows.OfType<ComposerWindow>().Any(w => w.IsVisible) || host.Windows.OfType<SnoozeWindow>().Any(w => w.IsVisible);

    internal async System.Threading.Tasks.Task Run(bool manual = false)
    {
        if (busy || !Enabled || host.Exiting || Editing || (!manual && host.Manager?.IsEnabled == false)) return;
        busy = true; Error = ""; Changed?.Invoke();
        int token = generation; var options = Options;
        var snapshot = CloudSync.Copy(host.Data);
        string fingerprint = CloudSync.Fingerprint(snapshot);
        string backupFingerprint = CloudSync.Fingerprint(CloudSync.Backup(snapshot));
        try
        {
            var merged = await System.Threading.Tasks.Task.Run(() =>
            {
                if (!Directory.Exists(options.Folder)) throw new DirectoryNotFoundException();
                string devices = Path.Combine(options.Folder, "devices");
                if (!options.Sync) return (SyncDocument?)null;
                var docs = new List<SyncDocument> { CloudSync.Capture(snapshot, options.Device) };
                if (Directory.Exists(devices))
                {
                    var files = Directory.GetFiles(devices, "*.json");
                    if (files.Length > 100) throw new InvalidDataException("Too many device files.");
                    if (files.Sum(f => new FileInfo(f).Length) > 100L * 1024 * 1024) throw new InvalidDataException("Shared device files exceed the supported size.");
                    foreach (var file in files) docs.Add(CloudSync.Read(file));
                }
                return CloudSync.Merge(docs.ToArray());
            });
            if (token != generation || host.Exiting || Editing || (!manual && host.Manager?.IsEnabled == false) || fingerprint != CloudSync.Fingerprint(host.Data)) return;
            var result = merged is null ? snapshot : CloudSync.Apply(snapshot, merged);
            bool changed = CloudSync.ContentFingerprint(snapshot) != CloudSync.ContentFingerprint(result);
            // Keep a local recovery copy before any remote replacement. Commit data and
            // merge metadata together, before publishing a document another PC can see.
            if (changed) CloudSync.WriteBackup(Path.Combine(host.Storage.DirectoryPath, "sync-recovery"), options.Device, snapshot);
            if (merged is not null)
            {
                host.Storage.Save(result);
                if (changed) { host.Data = result; host.CloseReminder(); host.NotifyCloudChange(); }
                else host.Data.SyncState = merged;
            }
            await System.Threading.Tasks.Task.Run(() =>
            {
                if (token != generation || host.Exiting) return;
                if (!Directory.Exists(options.Folder)) throw new DirectoryNotFoundException();
                if (options.Backups && backupFingerprint != lastBackup)
                    CloudSync.WriteBackup(Path.Combine(options.Folder, "backups"), options.Device, snapshot);
                if (merged is not null)
                {
                    string devices = Path.Combine(options.Folder, "devices"); Directory.CreateDirectory(devices);
                    string file = Path.Combine(devices, options.Device + ".json");
                    string json = JsonSerializer.Serialize(merged, Store.Json);
                    if (!File.Exists(file) || File.ReadAllText(file) != json) CloudSync.WriteAtomic(file, merged);
                }
            });
            if (token == generation) { lastBackup = backupFingerprint; LastExchange = DateTime.Now; }
            if (changed && !host.TestMode) host.Tick();
        }
        catch (Exception e)
        {
            if (token == generation)
            {
                Error = L.T("The folder is unavailable, a file is still downloading, or its data needs checking. Nothing needs to be deleted; check your cloud app and try again. Details: ", "Ο φάκελος δεν είναι διαθέσιμος, κάποιο αρχείο κατεβαίνει ακόμη ή χρειάζεται έλεγχο. Δεν χρειάζεται διαγραφή· έλεγξε την εφαρμογή cloud και δοκίμασε ξανά. Λεπτομέρειες: ") + e.Message;
            }
        }
        finally { busy = false; Changed?.Invoke(); }
    }
}
