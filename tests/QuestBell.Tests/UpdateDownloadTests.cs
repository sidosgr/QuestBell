using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using QuestBell.Core;

internal static class UpdateDownloadTests
{
    internal static void Run(Action<string, bool> check)
    {
        byte[] bytes = Enumerable.Range(0, 150000).Select(i => (byte)(i % 251)).ToArray();
        string digest = Convert.ToHexString(SHA256.HashData(bytes));
        string url = GitHubUpdates.Repository + "/releases/download/v1.5.0/QuestBell-Setup-1.5.0.exe";
        var release = new UpdateRelease(new Version(1, 5, 0, 0), GitHubUpdates.Repository + "/releases/tag/v1.5.0", url, bytes.Length, digest);
        string root = Path.Combine(Path.GetTempPath(), "QuestBell-update-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            bool Reject(UpdateRelease item, Func<HttpRequestMessage, HttpResponseMessage> factory, CancellationToken token = default)
            {
                using var client = new HttpClient(new Handler(factory));
                var before = Directory.GetDirectories(root).ToHashSet();
                try { UpdateDownload.Fetch(client, item, root, token: token).GetAwaiter().GetResult(); return false; }
                catch (Exception e) when (e is InvalidDataException or HttpRequestException or OperationCanceledException)
                { return Directory.GetDirectories(root).All(before.Contains); }
            }
            HttpResponseMessage Good(HttpRequestMessage _) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            check("Direct update requires digest and size", release.CanInstall && !(release with { Sha256 = null }).CanInstall && !(release with { Size = 0 }).CanInstall);
            check("Direct update rejects excessive size and malformed hash", !(release with { Size = UpdateDownload.MaximumSize + 1 }).CanInstall && !(release with { Sha256 = "bad" }).CanInstall);
            string payload = JsonSerializer.Serialize(new { draft = false, prerelease = false, tag_name = "v1.5.0", html_url = release.Page, assets = new[] { new { name = "QuestBell-Setup-1.5.0.exe", state = "uploaded", browser_download_url = url, size = bytes.Length, digest = "sha256:" + digest } } });
            check("Release API digest and size enable direct installation", GitHubUpdates.Parse(payload, new Version(1, 4, 0))?.CanInstall == true);
            using (var client = new HttpClient(new Handler(Good)))
            {
                var progress = new Percentages(); string path = UpdateDownload.Fetch(client, release, root, progress).GetAwaiter().GetResult();
                check("Verified update bytes are streamed into executable only after verification", File.ReadAllBytes(path).SequenceEqual(bytes) && !File.Exists(path + ".partial"));
                check("Download reports completion", progress.Values.Last() == 100 && progress.Values.SequenceEqual(progress.Values.Order()));
            }
            check("Checksum mismatch rejected and partial files removed", Reject(release with { Sha256 = new string('0', 64) }, Good));
            check("Wrong declared size rejected and cleaned", Reject(release with { Size = bytes.Length + 1 }, Good));
            HttpResponseMessage UnknownLength(byte[] data) { var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(data)) }; response.Content.Headers.ContentLength = null; return response; }
            check("Truncated stream without content length rejected", Reject(release, _ => UnknownLength(bytes[..500])));
            check("Oversized stream without content length rejected", Reject(release with { Size = 100 }, _ => UnknownLength(bytes)));
            check("HTTP failure leaves no executable", Reject(release, _ => new(HttpStatusCode.NotFound)));
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            check("Cancelled download cleaned without executable", Reject(release, Good, cancelled.Token));
            HttpResponseMessage Redirect(string destination) { var response = new HttpResponseMessage(HttpStatusCode.Redirect); response.Headers.Location = new Uri(destination); return response; }
            check("Foreign download redirect rejected", Reject(release, _ => Redirect("https://example.com/evil.exe")));
            check("Insecure HTTP redirect rejected", Reject(release, _ => Redirect("http://release-assets.githubusercontent.com/file")));
            check("Redirect loop is bounded", Reject(release, _ => Redirect(url)));
            check("Foreign initial installer rejected", Reject(release with { Installer = "https://example.com/QuestBell-Setup-1.5.0.exe" }, Good));
            check("Installer version mismatch rejected", Reject(release with { Version = new Version(1, 6, 0, 0) }, Good));
            int requests = 0;
            using (var client = new HttpClient(new Handler(request => { requests++; return request.RequestUri!.Host == "github.com" ? Redirect("https://release-assets.githubusercontent.com/github-production-release-asset/file?token=fixture") : Good(request); })))
            {
                string path = UpdateDownload.Fetch(client, release, root).GetAwaiter().GetResult();
                check("GitHub asset redirect downloads and verifies successfully", requests == 2 && File.ReadAllBytes(path).SequenceEqual(bytes));
            }
            check("Redirect hosts reject lookalikes, credentials and alternate ports", !UpdateDownload.TrustedDestination(new Uri("https://release-assets.githubusercontent.com.evil/file")) && !UpdateDownload.TrustedDestination(new Uri("https://user@github.com/file")) && !UpdateDownload.TrustedDestination(new Uri("https://github.com:444/file")));
        }
        finally { Directory.Delete(root, true); }
    }
    private sealed class Percentages : IProgress<int> { internal List<int> Values = []; public void Report(int value) => Values.Add(value); }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { token.ThrowIfCancellationRequested(); return System.Threading.Tasks.Task.FromResult(factory(request)); }
    }
}
