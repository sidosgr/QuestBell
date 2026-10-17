using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace QuestBell.Core;

public sealed class SyncEntry
{
    public long Counter { get; set; }
    public string Device { get; set; } = "";
    public string? Value { get; set; }
}

public sealed class SyncDocument
{
    public int Version { get; set; } = 1;
    public Dictionary<string, SyncEntry> Entries { get; set; } = new();
}

public static class CloudSync
{
    private static readonly JsonSerializerOptions Compact = new();
    public static AppData Copy(AppData data) => JsonSerializer.Deserialize<AppData>(JsonSerializer.Serialize(data))!;
    public static AppData Backup(AppData data) { var copy = Copy(data); copy.SyncState = null; return copy; }
    public static string Fingerprint(AppData data) => JsonSerializer.Serialize(data, Compact);
    public static string ContentFingerprint(AppData data) => JsonSerializer.Serialize(Records(data).OrderBy(x => x.Key, StringComparer.Ordinal), Compact);

    private static Dictionary<string, string> Records(AppData data)
    {
        var records = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var task in data.Reminders)
        {
            var copy = task.Clone(); copy.LastArtVariant = -1; copy.FollowUpAt = null;
            // Delivery artwork is local; rotating it must not defeat a remote completion.
            if (!copy.PinArt) copy.ArtVariant = -1;
            records.Add("r:" + task.Id, JsonSerializer.Serialize(copy, Compact));
        }
        foreach (var entry in data.History) records.TryAdd("h:" + entry.Id, JsonSerializer.Serialize(entry, Compact));
        foreach (var (kind, words) in data.CustomPhrases)
        foreach (var word in words.Distinct())
            records.Add("p:" + kind + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(word))), JsonSerializer.Serialize(word));
        return records;
    }

    public static SyncDocument Capture(AppData data, string device)
    {
        if (!Guid.TryParseExact(device, "N", out _)) throw new InvalidDataException("Invalid device identifier.");
        data.Validate();
        var doc = data.SyncState is null ? new SyncDocument() : JsonSerializer.Deserialize<SyncDocument>(JsonSerializer.Serialize(data.SyncState))!;
        Validate(doc);
        if (data.Reminders.Any(r => r.IsWork) || data.History.Any(h => h.Before.IsWork)) doc.Version = 2;
        var records = Records(data);
        long counter = checked(doc.Entries.Values.Select(x => x.Counter).DefaultIfEmpty(0).Max() + 1);
        foreach (var key in doc.Entries.Keys.Union(records.Keys).ToArray())
        {
            records.TryGetValue(key, out string? value);
            if (!doc.Entries.TryGetValue(key, out var old) || old.Value != value)
                doc.Entries[key] = new SyncEntry { Counter = counter, Device = device, Value = value };
        }
        return doc;
    }

    public static SyncDocument Merge(params SyncDocument[] documents)
    {
        var result = new SyncDocument { Version = documents.Any(d => d.Version == 2) ? 2 : 1 };
        foreach (var doc in documents)
        {
            Validate(doc);
            foreach (var (key, entry) in doc.Entries)
            {
                if (!result.Entries.TryGetValue(key, out var old) || Compare(entry, old) > 0) result.Entries[key] = entry;
            }
        }
        result.Entries = result.Entries.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        Validate(result);
        return result;
    }
    private static int Compare(SyncEntry a, SyncEntry b)
    {
        int order = a.Counter.CompareTo(b.Counter);
        if (order == 0) order = string.CompareOrdinal(a.Device, b.Device);
        // The same stamp with different content indicates a damaged/conflicted document.
        if (order == 0 && a.Value != b.Value) throw new InvalidDataException("Conflicting sync record with the same version.");
        return order;
    }

    public static AppData Apply(AppData local, SyncDocument document)
    {
        Validate(document);
        var result = Copy(local); result.Reminders.Clear(); result.History.Clear();
        foreach (var kind in new[] { "action", "subject", "object" }) result.CustomPhrases[kind] = [];
        foreach (var (key, entry) in document.Entries.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (entry.Value is null) continue;
            if (key.StartsWith("r:"))
            {
                var task = JsonSerializer.Deserialize<Reminder>(entry.Value)!;
                var previous = local.Reminders.FirstOrDefault(x => x.Id == task.Id);
                if (previous is not null) { task.LastArtVariant = previous.LastArtVariant; if (!task.PinArt && !previous.PinArt && task.Category == previous.Category) task.ArtVariant = previous.ArtVariant; }
                result.Reminders.Add(task);
            }
            else if (key.StartsWith("h:")) result.History.Add(JsonSerializer.Deserialize<HistoryEntry>(entry.Value)!);
            else result.CustomPhrases[key.Split(':')[1]].Add(JsonSerializer.Deserialize<string>(entry.Value)!);
        }
        result.History = result.History.OrderByDescending(x => x.At).ThenBy(x => x.Id, StringComparer.Ordinal).ToList();
        result.SyncState = document; result.Validate(); return result;
    }

    public static void Validate(SyncDocument doc)
    {
        if (doc is null || doc.Version is not (1 or 2) || doc.Entries is null || doc.Entries.Count > 250000) throw new InvalidDataException("Unsupported or oversized sync data.");
        var sample = new AppData();
        foreach (var (key, entry) in doc.Entries)
        {
            if (key.Length > 300 || entry is null || entry.Counter < 1 || entry.Counter > long.MaxValue - 1000000 || !Guid.TryParseExact(entry.Device, "N", out _)) throw new InvalidDataException("Invalid sync record.");
            bool reminder = key.StartsWith("r:"), history = key.StartsWith("h:");
            var parts = key.Split(':');
            bool phrase = parts.Length == 3 && parts[0] == "p" && sample.CustomPhrases.ContainsKey(parts[1]);
            if (!reminder && !history && !phrase) throw new InvalidDataException("Unknown sync record.");
            if (entry.Value is null) continue;
            if (reminder)
            {
                var task = JsonSerializer.Deserialize<Reminder>(entry.Value) ?? throw new InvalidDataException("Empty reminder.");
                if (key != "r:" + task.Id) throw new InvalidDataException("Reminder identifier mismatch.");
                sample.Reminders.Add(task);
            }
            else if (history)
            {
                var item = JsonSerializer.Deserialize<HistoryEntry>(entry.Value) ?? throw new InvalidDataException("Empty history.");
                if (key != "h:" + item.Id) throw new InvalidDataException("History identifier mismatch.");
                sample.History.Add(item);
            }
            else
            {
                var word = JsonSerializer.Deserialize<string>(entry.Value) ?? throw new InvalidDataException("Empty phrase.");
                if (parts[2] != Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(word)))) throw new InvalidDataException("Phrase identifier mismatch.");
                sample.CustomPhrases[parts[1]].Add(word);
            }
        }
        sample.Validate();
    }

    public static SyncDocument Read(string path)
    {
        if (new FileInfo(path).Length > 100 * 1024 * 1024) throw new InvalidDataException("Sync file is too large.");
        var doc = JsonSerializer.Deserialize<SyncDocument>(File.ReadAllText(path), Store.Json) ?? throw new InvalidDataException("Empty sync file.");
        Validate(doc); return doc;
    }

    public static void WriteAtomic<T>(string path, T value)
    {
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { JsonSerializer.Serialize(stream, value, Store.Json); stream.Flush(true); }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static string WriteBackup(string folder, string device, AppData data)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, device + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + "-" + Guid.NewGuid().ToString("N") + ".json");
        WriteAtomic(path, Backup(data));
        foreach (var old in new DirectoryInfo(folder).GetFiles(device + "-*.json").OrderByDescending(f => f.Name, StringComparer.Ordinal).Skip(30)) old.Delete();
        return path;
    }
}
