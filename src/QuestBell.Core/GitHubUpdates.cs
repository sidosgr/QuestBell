using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QuestBell.Core;

public record UpdateRelease(Version Version, string Page, string? Installer);
public static class GitHubUpdates
{
    public const string Repository = "https://github.com/sidosgr/QuestBell";
    public const string Endpoint = "https://api.github.com/repos/sidosgr/QuestBell/releases/latest";
    public static bool TryVersion(string? tag, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (tag is null || !Regex.IsMatch(tag, @"^v?\d+\.\d+\.\d+(\.\d+)?$", RegexOptions.IgnoreCase) || !Version.TryParse(tag.TrimStart('v', 'V'), out var parsed)) return false;
        version = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build), Math.Max(0, parsed.Revision)); return true;
    }
    public static UpdateRelease? Parse(string json, Version installed)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        string? tag = root.GetProperty("tag_name").GetString();
        if (!TryVersion(tag, out var version)) throw new InvalidDataException("Release tags must use vMAJOR.MINOR.PATCH.");
        var current = new Version(installed.Major, installed.Minor, Math.Max(0, installed.Build), Math.Max(0, installed.Revision));
        if (version <= current) return null;
        string expected = Repository + "/releases/tag/" + Uri.EscapeDataString(tag!);
        string? page = root.GetProperty("html_url").GetString();
        if (!string.Equals(page, expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unexpected release destination.");
        string? installer = null;
        string name = "QuestBell-Setup-" + version.ToString(version.Revision == 0 ? 3 : 4) + ".exe";
        if (root.TryGetProperty("assets", out var assets))
        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != name) continue;
            string url = Repository + "/releases/download/" + Uri.EscapeDataString(tag!) + "/" + name;
            if (string.Equals(asset.GetProperty("browser_download_url").GetString(), url, StringComparison.OrdinalIgnoreCase) && asset.GetProperty("state").GetString() == "uploaded") installer = url;
        }
        return new UpdateRelease(version, page!, installer);
    }
    public static async Task<(bool Unpublished, UpdateRelease? Release)> Check(HttpClient client, Version installed, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.UserAgent.ParseAdd("QuestBell/" + installed.ToString(3));
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return (true, null);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests) throw new HttpRequestException("GitHub temporarily limited update checks. Try again later.");
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (json.Length > 2 * 1024 * 1024) throw new InvalidDataException("Release response too large.");
        return (false, Parse(json, installed));
    }
}
