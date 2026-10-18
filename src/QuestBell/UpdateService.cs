using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Threading;

namespace QuestBell;

internal sealed class UpdateService : IDisposable
{
    private readonly App host;
    private readonly HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMinutes(2) };
    private readonly string stampPath;
    private CancellationTokenSource? download;
    private bool cancelledByUser;
    internal bool Downloading => download is not null;
    internal int Percent { get; private set; }
    private DateTime lastAttempt;
    private string state = "idle";
    internal bool Busy { get; private set; }
    internal UpdateRelease? Available { get; private set; }
    internal event Action? Changed;
    internal static Version Installed => typeof(App).Assembly.GetName().Version!;
    internal string Status => state switch
    {
        "checking" => L.T("Checking GitHub…", "Έλεγχος στο GitHub…"),
        "downloading" => L.T("Downloading update… ", "Λήψη ενημέρωσης… ") + Percent + "%",
        "installing" => L.T("Installing update… QuestBell will reopen.", "Εγκατάσταση ενημέρωσης… Το QuestBell θα ανοίξει ξανά."),
        "cancelled" => L.T("Download cancelled. You can update later.", "Η λήψη ακυρώθηκε. Μπορείς να ενημερώσεις αργότερα."),
        "install-error" => L.T("Update could not finish. Nothing unverified was installed. Retry or open GitHub releases.", "Η ενημέρωση δεν ολοκληρώθηκε. Δεν εγκαταστάθηκε μη επαληθευμένο αρχείο. Δοκίμασε ξανά ή άνοιξε τις εκδόσεις στο GitHub."),
        "available" => L.T("Update available: ", "Διαθέσιμη ενημέρωση: ") + Available!.Version.ToString(3),
        "current" => L.T("You have the latest published version.", "Έχεις την πιο πρόσφατη δημοσιευμένη έκδοση."),
        "unpublished" => L.T("No public release is available yet.", "Δεν υπάρχει ακόμη δημόσια έκδοση."),
        "error" => L.T("Could not check for updates. Check your connection or try again later.", "Δεν έγινε έλεγχος ενημερώσεων. Έλεγξε τη σύνδεση ή δοκίμασε αργότερα."),
        _ => L.T("Updates from sidosgr/QuestBell on GitHub", "Ενημερώσεις από το sidosgr/QuestBell στο GitHub")
    };
    internal UpdateService(App app)
    {
        host = app; stampPath = Path.Combine(host.Storage.DirectoryPath, "update-check.json");
        CleanOldDownloads();
        try { if (File.Exists(stampPath)) lastAttempt = JsonSerializer.Deserialize<DateTime>(File.ReadAllText(stampPath)); } catch { }
        if (lastAttempt > DateTime.UtcNow) lastAttempt = DateTime.MinValue;
        timer.Tick += async (_, _) =>
        {
            timer.Interval = TimeSpan.FromHours(1);
            if (host.Data.Settings.CheckUpdates && DateTime.UtcNow - lastAttempt >= TimeSpan.FromHours(24)) await Check();
        };
    }
    internal void Start() { if (!host.TestMode) timer.Start(); }
    internal async System.Threading.Tasks.Task Check()
    {
        if (Busy || host.Exiting) return;
        Busy = true; state = "checking"; Changed?.Invoke();
        try
        {
            lastAttempt = DateTime.UtcNow;
            try { CloudSync.WriteAtomic(stampPath, lastAttempt); } catch { }
            var result = await GitHubUpdates.Check(client, Installed);
            Available = result.Release; state = Available is not null ? "available" : result.Unpublished ? "unpublished" : "current";
        }
        catch { state = Available is not null ? "available" : "error"; }
        finally { Busy = false; if (!host.Exiting) Changed?.Invoke(); }
    }
    internal void OpenRelease()
    {
        Process.Start(new ProcessStartInfo(Available?.Page ?? GitHubUpdates.Repository + "/releases") { UseShellExecute = true });
    }
    internal void CancelDownload() { if (download is not null) { cancelledByUser = true; download.Cancel(); } }
    private void CleanOldDownloads()
    {
        try
        {
            string root = Path.Combine(host.Storage.DirectoryPath, "Updates");
            if (!Directory.Exists(root)) return;
            foreach (var folder in new DirectoryInfo(root).EnumerateDirectories())
            {
                if (!Guid.TryParseExact(folder.Name, "N", out _) || folder.Attributes.HasFlag(FileAttributes.ReparsePoint) || folder.LastWriteTimeUtc > DateTime.UtcNow.AddDays(-2)) continue;
                foreach (var file in folder.EnumerateFiles())
                    if (System.Text.RegularExpressions.Regex.IsMatch(file.Name, @"^QuestBell-Setup-\d+\.\d+\.\d+(\.\d+)?\.exe(\.partial)?$")) file.Delete();
                if (!folder.EnumerateFileSystemInfos().Any()) folder.Delete();
            }
        }
        catch { }
    }
    internal async System.Threading.Tasks.Task Install()
    {
        if (Busy || host.Exiting || Available?.CanInstall != true || host.TestMode) return;
        Busy = true; Percent = 0; cancelledByUser = false; state = "downloading";
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        download = cancellation; Changed?.Invoke();
        string? file = null;
        try
        {
            using var transfer = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
            file = await UpdateDownload.Fetch(transfer, Available, Path.Combine(host.Storage.DirectoryPath, "Updates"), new Progress<int>(value =>
            { if (!host.Exiting && download is not null) { Percent = value; Changed?.Invoke(); } }), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            host.Save();
            download = null; state = "installing"; Changed?.Invoke();
            var start = new ProcessStartInfo(file) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(file)! };
            start.ArgumentList.Add("/update");
            start.ArgumentList.Add("/dir=" + AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            start.ArgumentList.Add(host.Data.Settings.AutoStart ? "/autostart=1" : "/autostart=0");
            if (L.Language == "el") start.ArgumentList.Add("/greek");
            using var setup = Process.Start(start) ?? throw new IOException("Installer did not start.");
            // Setup asks the current app to quit only when it is ready to replace files.
            await setup.WaitForExitAsync();
            if (!host.Exiting) state = "install-error";
        }
        catch (OperationCanceledException) { state = cancelledByUser ? "cancelled" : "install-error"; }
        catch { state = "install-error"; }
        finally
        {
            download = null; Busy = false;
            if (file is not null) { try { File.Delete(file); Directory.Delete(Path.GetDirectoryName(file)!); } catch { } }
            if (!host.Exiting) Changed?.Invoke();
        }
    }
    public void Dispose() { timer.Stop(); download?.Cancel(); client.Dispose(); }
}
