using System.Text;
using System.Text.Json;

namespace QuestBell.Core;

public sealed class Store
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public string DirectoryPath { get; }
    public string FilePath => Path.Combine(DirectoryPath, "questbell.json");
    public bool Recovered { get; private set; }
    public Store(string directory) { DirectoryPath = directory; Directory.CreateDirectory(directory); }

    public AppData Load()
    {
        if (!File.Exists(FilePath)) return new();
        try { return Read(FilePath); }
        catch (Exception e) when (e is JsonException or InvalidDataException)
        {
            // Preserve damaged data. Never silently overwrite it with an empty database.
            string preserved = FilePath + ".damaged-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
            File.Copy(FilePath, preserved);
            if (!File.Exists(FilePath + ".bak")) throw new InvalidDataException("Data could not be read. Original preserved at: " + preserved, e);
            var backup = Read(FilePath + ".bak");
            Recovered = true;
            return backup;
        }
    }

    public static AppData Read(string path)
    {
        if (new FileInfo(path).Length > 100 * 1024 * 1024) throw new InvalidDataException("Backup is too large.");
        var data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(path, Encoding.UTF8), Json)
                   ?? throw new InvalidDataException("Empty backup.");
        data.Validate();
        QuickOptions.UpgradeDefaultTimes(data.Settings);
        return data;
    }

    public void Save(AppData data)
    {
        data.Validate();
        var temp = FilePath + ".new";
        using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(file, data, Json);
            file.Flush(true);
        }
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak", true);
        else File.Move(temp, FilePath);
    }
}
