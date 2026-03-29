using MotoBlackBoxViewer.App.Controls;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.App.Services;
using MotoBlackBoxViewer.Core.Models;
using MotoBlackBoxViewer.Core.Services;

namespace MotoBlackBoxViewer.Tests;

public sealed class ChartPerformanceDiagnosticsTests
{
    [Fact]
    public void DownsampleValues_OnLargeInput_EmitsPerformanceEvent()
    {
        double[] values = Enumerable.Range(1, 10000).Select(static value => (double)value).ToArray();
        List<ChartPerformanceEvent> events = [];

        using IDisposable _ = ChartPerformanceDiagnostics.PushListener(events.Add);

        ChartDownsamplingHelper.DownsampleValues(values, selectedIndex: 5000, maxPointCount: 600);

        ChartPerformanceEvent evt = Assert.Single(events);
        Assert.Equal("DownsampleValues", evt.Operation);
        Assert.Equal(10000, evt.InputPointCount);
        Assert.True(evt.OutputPointCount > 0);
    }

    [Fact]
    public void CreateVisibleData_OnLargeInput_EmitsPerformanceEvent()
    {
        List<TelemetryPoint> points = Enumerable.Range(1, 7000)
            .Select(static index => new TelemetryPoint
            {
                Index = index,
                Latitude = 43 + (index * 0.0001),
                Longitude = 131 + (index * 0.0001),
                SpeedKmh = index % 180,
                LeanAngleDeg = (index % 50) - 25,
                AccelX = index * 0.01,
                AccelY = index * 0.02,
                AccelZ = index * 0.03,
                DistanceFromStartMeters = index * 2
            })
            .ToList();

        TelemetryDataProcessor processor = new(new TelemetryAnalyzer());
        List<ChartPerformanceEvent> events = [];

        using IDisposable _ = ChartPerformanceDiagnostics.PushListener(events.Add);

        TelemetryVisibleData visibleData = processor.CreateVisibleData(points, 100, 6900);

        Assert.Equal(6801, visibleData.Points.Count);
        ChartPerformanceEvent evt = Assert.Single(events);
        Assert.Equal("CreateVisibleData", evt.Operation);
        Assert.Equal(7000, evt.InputPointCount);
        Assert.Equal(6801, evt.OutputPointCount);
    }
}
