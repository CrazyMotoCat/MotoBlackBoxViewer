using System.Net;
using System.Net.Http;
using MotoBlackBoxViewer.App.Services;

namespace MotoBlackBoxViewer.Tests;

public sealed class OpenStreetMapTileCacheServiceTests : IDisposable
{
    private readonly string _cacheRoot = Path.Combine(Path.GetTempPath(), "MotoBlackBoxViewer.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void TryParseTileRequest_AcceptsStandardOpenStreetMapTileUrls()
    {
        bool parsed = OpenStreetMapTileCacheService.TryParseTileRequest(
            new Uri("https://b.tile.openstreetmap.org/12/3456/7890.png"),
            out OpenStreetMapTileRequest request);

        Assert.True(parsed);
        Assert.Equal("b.tile.openstreetmap.org", request.Host);
        Assert.Equal(12, request.Zoom);
        Assert.Equal(3456, request.X);
        Assert.Equal(7890, request.Y);
        Assert.Equal(".png", request.Extension);
    }

    [Theory]
    [InlineData("http://tile.openstreetmap.org/12/3456/7890.png")]
    [InlineData("https://example.com/12/3456/7890.png")]
    [InlineData("https://tile.openstreetmap.org/12/3456/not-a-number.png")]
    [InlineData("https://tile.openstreetmap.org/12/3456/7890.jpg")]
    public void TryParseTileRequest_RejectsUnsupportedUrls(string url)
    {
        bool parsed = OpenStreetMapTileCacheService.TryParseTileRequest(
            new Uri(url),
            out OpenStreetMapTileRequest request);

        Assert.False(parsed);
        Assert.Equal(default, request);
    }

    [Fact]
    public async Task GetTileAsync_DownloadsTileAndThenServesItFromCache()
    {
        byte[] tileBytes = [1, 2, 3, 4];
        StubHttpMessageHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(tileBytes)
        });

        using OpenStreetMapTileCacheService cache = new(_cacheRoot, handler);

        OpenStreetMapTileResponse? first = await cache.GetTileAsync(new Uri("https://tile.openstreetmap.org/8/150/200.png"));
        OpenStreetMapTileResponse? second = await cache.GetTileAsync(new Uri("https://tile.openstreetmap.org/8/150/200.png"));

        Assert.NotNull(first);
        Assert.False(first.Value.FromCache);
        Assert.Equal("image/png", first.Value.MimeType);
        Assert.Equal(tileBytes, first.Value.Content);

        Assert.NotNull(second);
        Assert.True(second.Value.FromCache);
        Assert.Equal(tileBytes, second.Value.Content);
        Assert.Equal(1, handler.CallCount);
        Assert.True(File.Exists(second.Value.CachePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheRoot))
            Directory.Delete(_cacheRoot, recursive: true);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory = responseFactory;

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_responseFactory(request));
        }
    }
}
