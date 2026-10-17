using System.Net;
using System.Text.Json;
using QuestBell.Core;

internal static class UpdateTests
{
    internal static void Run(Action<string, bool> check)
    {
        var installed = new Version(1, 3, 1, 0);
        string Payload(string tag = "v1.4.0", bool draft = false, bool prerelease = false, string? page = null, string? asset = null) => JsonSerializer.Serialize(new
        {
            tag_name = tag, draft, prerelease, html_url = page ?? GitHubUpdates.Repository + "/releases/tag/" + tag,
            assets = new[] { new { name = "QuestBell-Setup-1.4.0.exe", state = "uploaded", browser_download_url = asset ?? GitHubUpdates.Repository + "/releases/download/v1.4.0/QuestBell-Setup-1.4.0.exe" } }
        });
        check("Update v-prefixed version accepted", GitHubUpdates.TryVersion("v1.4.0", out var version) && version == new Version(1, 4, 0, 0));
        check("Update plain version accepted", GitHubUpdates.TryVersion("1.4.0", out _));
        check("Update four-component version accepted", GitHubUpdates.TryVersion("v1.4.0.2", out _));
        check("Update preview version excluded", !GitHubUpdates.TryVersion("v1.4.0-beta.1", out _));
        check("Update invalid or oversized version excluded", !GitHubUpdates.TryVersion("release", out _) && !GitHubUpdates.TryVersion("v999999999999.0.0", out _));
        var release = GitHubUpdates.Parse(Payload(), installed);
        check("Update newer stable release detected", release?.Version == new Version(1, 4, 0, 0));
        check("Update owner and repository casing accepted", GitHubUpdates.Parse(Payload(page: "https://github.com/SidosGR/QuestBell/releases/tag/v1.4.0"), installed) is not null);
        check("Update installer URL matches repository and version", release?.Installer == GitHubUpdates.Repository + "/releases/download/v1.4.0/QuestBell-Setup-1.4.0.exe");
        check("Update equal release excluded despite version component count", GitHubUpdates.Parse(Payload("v1.3.1"), installed) is null);
        check("Update older release excluded", GitHubUpdates.Parse(Payload("v1.2.0"), installed) is null);
        check("Update compares numbers rather than text", GitHubUpdates.Parse(Payload("v1.10.0"), new Version(1, 9, 0)) is not null);
        check("Update drafts and prereleases excluded", GitHubUpdates.Parse(Payload(draft: true), installed) is null && GitHubUpdates.Parse(Payload(prerelease: true), installed) is null);
        bool Reject(Action action) { try { action(); return false; } catch (Exception e) when (e is InvalidDataException or JsonException or KeyNotFoundException) { return true; } }
        check("Update foreign release destination rejected", Reject(() => GitHubUpdates.Parse(Payload(page: "https://example.com/installer"), installed)));
        check("Update repository lookalike rejected", Reject(() => GitHubUpdates.Parse(Payload(page: GitHubUpdates.Repository + ".evil/releases/tag/v1.4.0"), installed)));
        check("Update foreign installer destination excluded", GitHubUpdates.Parse(Payload(asset: "https://example.com/file.exe"), installed)?.Installer is null);
        check("Update malformed response rejected", Reject(() => GitHubUpdates.Parse("{broken", installed)) && Reject(() => GitHubUpdates.Parse("{}", installed)));
        var handler = new FixtureHandler { Json = Payload() }; using var client = new HttpClient(handler);
        var response = GitHubUpdates.Check(client, installed).GetAwaiter().GetResult();
        check("Update HTTP release response handled", !response.Unpublished && response.Release is not null);
        check("Update request uses exact public endpoint with no body or credentials", handler.CorrectRequest);
        handler.Code = HttpStatusCode.NotFound;
        check("Update unpublished repository is handled", GitHubUpdates.Check(client, installed).GetAwaiter().GetResult().Unpublished);
        handler.Code = HttpStatusCode.Forbidden;
        bool networkRejected = false; try { GitHubUpdates.Check(client, installed).GetAwaiter().GetResult(); } catch (HttpRequestException) { networkRejected = true; }
        check("Update rate limit does not become an up-to-date result", networkRejected);
        handler.Code = HttpStatusCode.InternalServerError;
        networkRejected = false; try { GitHubUpdates.Check(client, installed).GetAwaiter().GetResult(); } catch (HttpRequestException) { networkRejected = true; }
        check("Update server failure does not become an up-to-date result", networkRejected);
        handler.Code = HttpStatusCode.OK; handler.Json = new string('x', 2 * 1024 * 1024 + 1);
        check("Update oversized response rejected", Reject(() => GitHubUpdates.Check(client, installed).GetAwaiter().GetResult()));
    }
    private sealed class FixtureHandler : HttpMessageHandler
    {
        internal string Json = ""; internal HttpStatusCode Code = HttpStatusCode.OK; internal bool CorrectRequest;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            CorrectRequest = request.RequestUri?.AbsoluteUri == GitHubUpdates.Endpoint && request.Method == HttpMethod.Get && request.Content is null && request.Headers.Authorization is null && request.Headers.UserAgent.ToString().StartsWith("QuestBell/") && request.Headers.Contains("X-GitHub-Api-Version");
            return System.Threading.Tasks.Task.FromResult(new HttpResponseMessage(Code) { Content = new StringContent(Json) });
        }
    }
}
