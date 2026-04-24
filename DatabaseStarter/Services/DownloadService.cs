using System.IO;
using System.Net.Http;

namespace DatabaseStarter.Services;

public class DownloadService
{
    private static readonly HttpClient Client;

    static DownloadService()
    {
        Client = new HttpClient();
        Client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public async Task DownloadFileAsync(string url, string destinationPath,
        IProgress<double> progress, CancellationToken ct)
    {
        var fallbackUrl = GetMySqlCdnFallbackUrl(url);
        var urlsToTry = fallbackUrl is null ? new[] { url } : new[] { url, fallbackUrl };

        HttpRequestException? lastHttpError = null;
        Exception? lastError = null;

        foreach (var candidateUrl in urlsToTry)
        {
            try
            {
                using var response = await Client.GetAsync(candidateUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var dir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(destinationPath, FileMode.Create,
                    FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                        progress.Report((double)totalRead / totalBytes * 100.0);
                }

                progress.Report(100.0);
                return;
            }
            catch (HttpRequestException ex) when (ShouldTryFallback(ex, candidateUrl, fallbackUrl))
            {
                lastHttpError = ex;
                lastError = ex;
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested && candidateUrl != fallbackUrl &&
                                                   fallbackUrl is not null)
            {
                // Timeout/network flake on dev.mysql.com: retry once via direct CDN URL.
                lastError = ex;
            }
        }

        if (lastHttpError is not null)
            throw lastHttpError;

        if (lastError is not null)
            throw lastError;

        throw new InvalidOperationException("Download konnte nicht gestartet werden.");
    }

    private static bool ShouldTryFallback(HttpRequestException ex, string candidateUrl, string? fallbackUrl)
    {
        if (fallbackUrl is null || candidateUrl == fallbackUrl)
            return false;

        var statusCode = (int?)ex.StatusCode;
        return statusCode is 403 or >= 500 || statusCode is null;
    }

    private static string? GetMySqlCdnFallbackUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        if (!string.Equals(uri.Host, "dev.mysql.com", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!uri.AbsolutePath.StartsWith("/get/Downloads/", StringComparison.OrdinalIgnoreCase))
            return null;

        var cdnPath = uri.AbsolutePath[4..]; // strip "/get"
        var fallbackUri = new UriBuilder(uri)
        {
            Host = "cdn.mysql.com",
            Path = cdnPath
        };

        return fallbackUri.Uri.AbsoluteUri;
    }
}