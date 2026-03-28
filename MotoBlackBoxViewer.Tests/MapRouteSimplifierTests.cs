using MotoBlackBoxViewer.App.Services;
using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.Tests;

public sealed class MapRouteSimplifierTests
{
    [Fact]
    public void Simplify_WhenPointCountExceedsLimit_ReturnsReducedRouteKeepingEndpoints()
    {
        TelemetryPoint[] points = Enumerable.Range(1, 20)
            .Select(index => new TelemetryPoint { Index = index, Latitude = 43 + index, Longitude = 131 + index })
            .ToArray();

        IReadOnlyList<TelemetryPoint> simplified = MapRouteSimplifier.Simplify(points, maxPointCount: 5);

        Assert.True(simplified.Count <= 6);
        Assert.Equal(1, simplified[0].Index);
        Assert.Equal(20, simplified[^1].Index);
    }
}
