using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace MotoBlackBoxViewer.App.Services;

internal sealed class OpenStreetMapTileCacheService : IDisposable
{
    private const string TileHostSuffix = ".tile.openstreetmap.org";
    private static readonly HttpClient SharedHttpClient = CreateSharedHttpClient();

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _tileLocks = new(StringComparer.OrdinalIgnoreCase);

    public OpenStreetMapTileCacheService(string? cacheRoot = null, HttpMessageHandler? handler = null)
    {
        CacheRoot = string.IsNullOrWhiteSpace(cacheRoot)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MotoBlackBoxViewer",
                "MapTileCache")
            : cacheRoot;

        Directory.CreateDirectory(CacheRoot);

        if (handler is null)
        {
            _httpClient = SharedHttpClient;
            return;
        }

        _httpClient = new HttpClient(handler, disposeHandler: true);
        ConfigureClient(_httpClient);
        _ownsHttpClient = true;
    }

    public string CacheRoot { get; }

    public bool TryGetCacheEntry(Uri uri, out OpenStreetMapTileCacheEntry entry)
    {
        if (TryParseTileRequest(uri, out OpenStreetMapTileRequest request))
        {
            string cachePath = Path.Combine(
                CacheRoot,
                request.Zoom.ToString(CultureInfo.InvariantCulture),
                request.X.ToString(CultureInfo.InvariantCulture),
                $"{request.Y}{request.Extension}");

            entry = new OpenStreetMapTileCacheEntry(request, cachePath, GetMimeType(request.Extension));
            return true;
        }

        entry = default;
        return false;
    }

    public async Task<OpenStreetMapTileResponse?> GetTileAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        if (!TryGetCacheEntry(uri, out OpenStreetMapTileCacheEntry entry))
            return null;

        if (TryReadCachedTile(entry, out OpenStreetMapTileResponse? cachedResponse))
            return cachedResponse;

        SemaphoreSlim tileLock = _tileLocks.GetOrAdd(entry.CachePath, static _ => new SemaphoreSlim(1, 1));
        await tileLock.WaitAsync(cancellationToken);
        try
        {
            if (TryReadCachedTile(entry, out cachedResponse))
                return cachedResponse;

            OpenStreetMapTileResponse? downloadedResponse = await DownloadAndCacheTileAsync(entry, cancellationToken);
            return downloadedResponse;
        }
        finally
        {
            tileLock.Release();
        }
    }

    public static bool TryParseTileRequest(Uri uri, out OpenStreetMapTileRequest request)
    {
        if (!uri.IsAbsoluteUri
            || !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !IsSupportedTileHost(uri.Host))
        {
            request = default;
            return false;
        }

        string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 3
            || !int.TryParse(segments[0], NumberStyles.None, CultureInfo.InvariantCulture, out int zoom)
            || !int.TryParse(segments[1], NumberStyles.None, CultureInfo.InvariantCulture, out int x))
        {
            request = default;
            return false;
        }

        string extension = Path.GetExtension(segments[2]);
        string yPart = Path.GetFileNameWithoutExtension(segments[2]);
        if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(yPart, NumberStyles.None, CultureInfo.InvariantCulture, out int y))
        {
            request = default;
            return false;
        }

        request = new OpenStreetMapTileRequest(uri.Host, zoom, x, y, extension.ToLowerInvariant());
        return true;
    }

    private static bool IsSupportedTileHost(string host)
    {
        return host.Equals("tile.openstreetmap.org", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(TileHostSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryReadCachedTile(OpenStreetMapTileCacheEntry entry, out OpenStreetMapTileResponse? response)
    {
        if (!File.Exists(entry.CachePath))
        {
            response = null;
            return false;
        }

        byte[] content = File.ReadAllBytes(entry.CachePath);
        if (content.Length == 0)
        {
            response = null;
            return false;
        }

        response = new OpenStreetMapTileResponse(content, entry.MimeType, entry.CachePath, FromCache: true);
        return true;
    }

    private async Task<OpenStreetMapTileResponse?> DownloadAndCacheTileAsync(
        OpenStreetMapTileCacheEntry entry,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, entry.Request.Uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));

        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (content.Length == 0)
            return null;

        string? directory = Path.GetDirectoryName(entry.CachePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string tempPath = $"{entry.CachePath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllBytesAsync(tempPath, content, cancellationToken);
        File.Move(tempPath, entry.CachePath, overwrite: true);

        return new OpenStreetMapTileResponse(content, entry.MimeType, entry.CachePath, FromCache: false);
    }

    private static string GetMimeType(string extension)
    {
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? "image/png"
            : "application/octet-stream";
    }

    private static HttpClient CreateSharedHttpClient()
    {
        HttpClient client = new();
        ConfigureClient(client);
        return client;
    }

    private static void ConfigureClient(HttpClient client)
    {
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MotoBlackBoxViewer/1.0");
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();

        foreach (SemaphoreSlim tileLock in _tileLocks.Values)
            tileLock.Dispose();

        _tileLocks.Clear();
    }
}

internal readonly record struct OpenStreetMapTileRequest(string Host, int Zoom, int X, int Y, string Extension)
{
    public Uri Uri => new($"https://{Host}/{Zoom}/{X}/{Y}{Extension}");
}

internal readonly record struct OpenStreetMapTileCacheEntry(
    OpenStreetMapTileRequest Request,
    string CachePath,
    string MimeType);

internal readonly record struct OpenStreetMapTileResponse(
    byte[] Content,
    string MimeType,
    string CachePath,
    bool FromCache);
