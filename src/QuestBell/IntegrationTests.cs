using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace QuestBell;

internal static class IntegrationTests
{
    internal static void Run(App app, string[] args)
    {
        app.Dispatcher.BeginInvoke(async () =>
        {
            Process? fixture = null; string command = Path.Combine(app.Storage.DirectoryPath, "fixture-command");
            var checks = new List<string>();
            void Check(string title, bool value) { if (!value) throw new Exception(title); checks.Add(title); }
            try
            {
                int index = Array.IndexOf(args, "--fixture"); string executable = args[index + 1];
                File.WriteAllText(command, "full");
                fixture = Process.Start(new ProcessStartInfo(executable, "\"" + command + "\"") { UseShellExecute = false, CreateNoWindow = true });
                for (int i = 0; i < 70 && !File.Exists(command + ".handle"); i++) await Task.Delay(100);
                var handle = new IntPtr(long.Parse(File.ReadAllText(command + ".handle")));
                Check("Real borderless full-screen window detected", Native.Fullscreen(handle));
                Check("Production EnumWindows callback marshals and discovers fixture", Native.FullscreenWindows().Contains(handle));
                // Keep the real production enumeration; filter unrelated user windows only
                // so another app on this computer cannot make the regression test flaky.
                app.ScanFullscreen = () => Native.FullscreenWindows().Where(h => h == handle).ToHashSet();
                var reminder = new Reminder { Title = "Full-screen regression", CustomTitle = true, Category = "bills", NextAt = DateTime.Now.AddMinutes(-1), AnchorAt = DateTime.Now.AddMinutes(-1), OccurrenceAt = DateTime.Now.AddMinutes(-1), Sound = false };
                app.Data.Reminders = [reminder]; app.Tick();
                Check("Due reminder opens behind full-screen window", app.CurrentReminder?.Id == reminder.Id && !app.IsPresenting && app.WaitingBlockers == 1);
                await Task.Delay(250); app.Tick();
                Check("Inactive full-screen window still blocks release", !app.IsPresenting && app.WaitingBlockers == 1);
                File.WriteAllText(command, "minimize"); await Task.Delay(350); app.Tick();
                Check("Minimizing or switching away does not release blocker", !app.IsPresenting && app.WaitingBlockers == 1);
                File.WriteAllText(command, "normal"); await Task.Delay(350); app.Tick();
                Check("Leaving full-screen on same window releases reminder", app.IsPresenting && app.WaitingBlockers == 0);
                var later = DateTime.Now.AddDays(3); app.Snooze(reminder, later);
                Check("Snooze closes current reminder and persists delay", app.CurrentReminder is null && app.Storage.Load().Reminders[0].NextAt == later);
                reminder.NextAt = DateTime.Now.AddSeconds(-1); app.ScanFullscreen = () => []; app.Tick();
                Check("Snoozed reminder returns when due", app.CurrentReminder?.Id == reminder.Id && app.IsPresenting);
                app.Pause(DateTime.Now.AddMinutes(30)); Check("Pause closes screen and keeps task pending", app.CurrentReminder is null && !reminder.Finished);
                app.Pause(null); Check("Resume releases pending task", app.CurrentReminder?.Id == reminder.Id);
                app.Resolve(reminder, "done"); Check("Done closes screen and completes one-off", app.CurrentReminder is null && reminder.Finished && app.Data.History.Count == 1);
                reminder.Finished = false; reminder.NextAt = DateTime.Now.AddSeconds(-1);
                app.ScanFullscreen = () => throw new InvalidOperationException("Deliberate regression-test scan failure");
                try { app.Tick(); } catch (InvalidOperationException) { }
                Check("A failed Windows scan leaves the reminder eligible for retry", app.CurrentReminder is null && !reminder.Finished);
                app.ScanFullscreen = Native.FullscreenWindows; app.Tick();
                Check("First reminder delivery recovers through the production Windows scan", app.CurrentReminder?.Id == reminder.Id);
                app.CloseReminder();
                File.WriteAllText(Path.Combine(app.Storage.DirectoryPath, "integration-report.json"), JsonSerializer.Serialize(new { Result = "PASS", Checks = checks }, Store.Json));
                app.Shutdown(0);
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(app.Storage.DirectoryPath, "integration-error.txt"), e.ToString()); app.Shutdown(4); }
            finally { app.CloseReminder(); if (fixture is not null && !fixture.HasExited) { File.WriteAllText(command, "quit"); await Task.Delay(250); } }
        }, DispatcherPriority.ApplicationIdle);
    }
    internal static void MeasureIdle(App app)
    {
        app.Dispatcher.BeginInvoke(async () =>
        {
            app.Data = new AppData(); app.Tick();
            await Task.Delay(3000); GC.Collect(); GC.WaitForPendingFinalizers();
            var process = Process.GetCurrentProcess(); process.Refresh(); double cpu = process.TotalProcessorTime.TotalMilliseconds;
            var start = DateTime.UtcNow;
            await Task.Delay(10000); process.Refresh();
            var report = new { WorkingSetMB = process.WorkingSet64 / 1048576.0, PrivateMB = process.PrivateMemorySize64 / 1048576.0, ManagedMB = GC.GetTotalMemory(false) / 1048576.0, CpuMillisecondsOver10Seconds = process.TotalProcessorTime.TotalMilliseconds - cpu, ElapsedSeconds = (DateTime.UtcNow - start).TotalSeconds, Note = "Cold scheduler-only process; no dashboard or artwork loaded." };
            File.WriteAllText(Path.Combine(app.Storage.DirectoryPath, "idle-report.json"), JsonSerializer.Serialize(report, Store.Json)); app.Shutdown(0);
        });
    }
}
