namespace QuestBell;

internal static class CloudServiceTests
{
    internal static async System.Threading.Tasks.Task<int> Run(App host)
    {
        int checks = 0;
        void Check(bool condition, string text) { if (!condition) throw new Exception("Cloud service: " + text); checks++; }
        var original = host.Data;
        var root = Path.Combine(Path.GetTempPath(), "QuestBell-folder-test-" + Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(root, "shared"); Directory.CreateDirectory(folder);
        const string otherDevice = "cccccccccccccccccccccccccccccccc";
        var date = DateTime.Today.AddDays(7);
        try
        {
            Check(!host.Cloud.Enabled, "disabled by default");
            await host.Cloud.Run(); Check(!Directory.EnumerateFileSystemEntries(folder).Any(), "disabled service writes nothing");
            host.Data = new AppData { Reminders = [new Reminder { Title = "Cloud test / Δοκιμή", NextAt = date, AnchorAt = date, OccurrenceAt = date }] };
            host.Cloud.Configure(folder, true, true); await host.Cloud.Run(true);
            Check(host.Cloud.Error.Length == 0 && host.Cloud.LastExchange.HasValue, "first folder exchange succeeds");
            string ownFile = Path.Combine(folder, "devices", host.Cloud.Options.Device + ".json");
            Check(File.Exists(ownFile) && Directory.GetFiles(Path.Combine(folder, "backups")).Length == 1, "sync and backup both written");
            var remote = CloudSync.Apply(new AppData(), CloudSync.Read(ownFile));
            remote.Reminders[0].Title = "Edited on PC two";
            remote.Reminders[0].Revision++;
            string otherFile = Path.Combine(folder, "devices", otherDevice + ".json");
            CloudSync.WriteAtomic(otherFile, CloudSync.Capture(remote, otherDevice));
            await host.Cloud.Run(true);
            Check(host.Data.Reminders[0].Title == "Edited on PC two" && host.Storage.Load().Reminders[0].Title == "Edited on PC two", "remote edit applied and persisted");
            var recovery = Directory.GetFiles(Path.Combine(host.Storage.DirectoryPath, "sync-recovery"), "*.json");
            Check(recovery.Any(p => Store.Read(p).Reminders.Any(r => r.Title == "Cloud test / Δοκιμή")), "local pre-merge copy preserved");
            string before = File.ReadAllText(ownFile);
            File.WriteAllText(otherFile, "{unfinished download"); await host.Cloud.Run(true);
            Check(host.Cloud.Error.Length > 0 && File.ReadAllText(ownFile) == before && host.Data.Reminders[0].Title == "Edited on PC two", "invalid remote data stops publication and preserves local data");
            host.Cloud.Configure(folder, false, true); await host.Cloud.Run(true);
            Check(host.Cloud.Error.Length == 0 && File.ReadAllText(ownFile) == before, "backup-only ignores remote sync and does not publish changes");
            var count = Directory.GetFiles(Path.Combine(folder, "backups")).Length;
            await host.Cloud.Run(true);
            Check(Directory.GetFiles(Path.Combine(folder, "backups")).Length == count, "unchanged data does not produce a backup every poll");
            host.Cloud.Pause(); host.Data.Reminders[0].Title = "Offline edit";
            await host.Cloud.Run(true);
            Check(File.ReadAllText(ownFile) == before && Directory.GetFiles(Path.Combine(folder, "backups")).Length == count, "turning off stops exchanges and retains files");
            File.WriteAllText(otherFile, System.Text.Json.JsonSerializer.Serialize(CloudSync.Capture(remote, otherDevice)));
            host.Cloud.Configure(folder, true, false); await host.Cloud.Run(true);
            Check(host.Cloud.Error.Length == 0 && host.Data.Reminders[0].Title == "Offline edit", "offline changes publish on reconnect");
            string moved = Path.Combine(root, "temporarily-unavailable"); Directory.Move(folder, moved);
            await host.Cloud.Run(true);
            Check(host.Cloud.Error.Length > 0 && !Directory.Exists(folder), "missing root is not silently recreated");
            Directory.Move(moved, folder); await host.Cloud.Run(true);
            Check(host.Cloud.Error.Length == 0, "folder reconnection recovers automatically");
            host.Cloud.Pause(); Check(!host.Cloud.Options.Sync && !host.Cloud.Options.Backups, "restore pause disables both modes");
        }
        finally
        {
            host.Cloud.Configure("", false, false); host.Data = original; host.Storage.Save(original);
        }
        return checks;
    }
}
