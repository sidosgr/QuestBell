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
    private DateTime lastAttempt;
    private string state = "idle";
    internal bool Busy { get; private set; }
    internal UpdateRelease? Available { get; private set; }
    internal event Action? Changed;
    internal static Version Installed => typeof(App).Assembly.GetName().Version!;
    internal string Status => state switch
    {
        "checking" => L.T("Checking GitHub…", "Έλεγχος στο GitHub…"),
        "available" => L.T("Update available: ", "Διαθέσιμη ενημέρωση: ") + Available!.Version.ToString(3),
        "current" => L.T("You have the latest published version.", "Έχεις την πιο πρόσφατη δημοσιευμένη έκδοση."),
        "unpublished" => L.T("No public release is available yet.", "Δεν υπάρχει ακόμη δημόσια έκδοση."),
        "error" => L.T("Could not check for updates. Check your connection or try again later.", "Δεν έγινε έλεγχος ενημερώσεων. Έλεγξε τη σύνδεση ή δοκίμασε αργότερα."),
        _ => L.T("Updates from sidosgr/QuestBell on GitHub", "Ενημερώσεις από το sidosgr/QuestBell στο GitHub")
    };
    internal UpdateService(App app)
    {
        host = app; stampPath = Path.Combine(host.Storage.DirectoryPath, "update-check.json");
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
    public void Dispose() { timer.Stop(); client.Dispose(); }
}
