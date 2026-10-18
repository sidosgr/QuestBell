using System.Net;
using System.Security.Cryptography;

namespace QuestBell.Core;

public static class UpdateDownload
{
    public const long MaximumSize = 512L * 1024 * 1024;
    public static bool TrustedDestination(Uri uri) => uri.Scheme == "https" && uri.Port == 443 && uri.UserInfo.Length == 0 &&
        (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase));

    // The caller supplies an HttpClient with automatic redirects disabled. Validate every hop.
    public static async Task<string> Fetch(HttpClient client, UpdateRelease release, string cacheRoot, IProgress<int>? progress = null, CancellationToken token = default)
    {
        string fileName = "QuestBell-Setup-" + release.Version.ToString(release.Version.Revision == 0 ? 3 : 4) + ".exe";
        if (!release.CanInstall || !Uri.TryCreate(release.Installer, UriKind.Absolute, out var uri) ||
            !release.Installer.StartsWith(GitHubUpdates.Repository + "/releases/download/", StringComparison.OrdinalIgnoreCase) ||
            uri.Segments.Last() != fileName || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidDataException("The release has no verifiable installer.");
        string directory = Path.Combine(cacheRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string partial = Path.Combine(directory, fileName + ".partial"), final = Path.Combine(directory, fileName);
        bool completed = false;
        try
        {
            for (int hop = 0; hop < 6; hop++)
            {
                if (!TrustedDestination(uri)) throw new InvalidDataException("Unexpected update download destination.");
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.UserAgent.ParseAdd("QuestBell-Updater");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                if (response.RequestMessage?.RequestUri is Uri actual && actual != uri) throw new InvalidDataException("Automatic update redirects must be disabled.");
                if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                {
                    uri = response.Headers.Location is Uri location ? new Uri(uri, location) : throw new InvalidDataException("Missing update redirect.");
                    continue;
                }
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is long declared && declared != release.Size) throw new InvalidDataException("Installer size does not match the release.");
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await using (var input = await response.Content.ReadAsStreamAsync(token))
                await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                {
                    var buffer = new byte[65536]; long total = 0; int last = -1;
                    while (true)
                    {
                        int count = await input.ReadAsync(buffer, token); if (count == 0) break;
                        total += count;
                        if (total > release.Size || total > MaximumSize) throw new InvalidDataException("Installer exceeds its published size.");
                        hash.AppendData(buffer, 0, count); await output.WriteAsync(buffer.AsMemory(0, count), token);
                        int percent = (int)(total * 100 / release.Size);
                        if (percent != last) { last = percent; progress?.Report(percent); }
                    }
                    if (total != release.Size) throw new InvalidDataException("Installer download is incomplete.");
                    if (!CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), Convert.FromHexString(release.Sha256!))) throw new InvalidDataException("Installer checksum does not match GitHub.");
                }
                token.ThrowIfCancellationRequested();
                File.Move(partial, final); completed = true; return final;
            }
            throw new InvalidDataException("Too many update redirects.");
        }
        finally
        {
            if (!completed) { try { Directory.Delete(directory, true); } catch { } }
        }
    }
}
