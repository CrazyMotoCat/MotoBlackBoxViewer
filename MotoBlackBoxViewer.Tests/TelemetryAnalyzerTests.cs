using MotoBlackBoxViewer.Core.Models;
using MotoBlackBoxViewer.Core.Services;

namespace MotoBlackBoxViewer.Tests;

public sealed class TelemetryAnalyzerTests
{
    [Fact]
    public void Analyze_ReturnsZeroStats_ForEmptyCollection()
    {
        var analyzer = new TelemetryAnalyzer();

        var stats = analyzer.Analyze(Array.Empty<TelemetryPoint>());

        Assert.Equal(0, stats.PointCount);
        Assert.Equal(0, stats.TotalDistanceMeters);
        Assert.Equal(0, stats.AverageSpeedKmh);
        Assert.Equal(0, stats.MaxSpeedKmh);
        Assert.Equal(0, stats.MinLeanDeg);
        Assert.Equal(0, stats.MaxLeanDeg);
    }

    [Fact]
    public void Analyze_ReturnsExpectedValues()
    {
        var analyzer = new TelemetryAnalyzer();
        var points = new[]
        {
            new TelemetryPoint { Index = 1, SpeedKmh = 10, LeanAngleDeg = 1.5, DistanceFromStartMeters = 0 },
            new TelemetryPoint { Index = 2, SpeedKmh = 20, LeanAngleDeg = 3.0, DistanceFromStartMeters = 15 },
            new TelemetryPoint { Index = 3, SpeedKmh = 30, LeanAngleDeg = -2.0, DistanceFromStartMeters = 40 }
        };

        var stats = analyzer.Analyze(points);

        Assert.Equal(3, stats.PointCount);
        Assert.Equal(40, stats.TotalDistanceMeters, 3);
        Assert.Equal(20, stats.AverageSpeedKmh, 3);
        Assert.Equal(30, stats.MaxSpeedKmh, 3);
        Assert.Equal(-2.0, stats.MinLeanDeg, 3);
        Assert.Equal(3.0, stats.MaxLeanDeg, 3);
    }

    [Fact]
    public void Analyze_UsesRelativeDistance_ForFilteredRange()
    {
        var analyzer = new TelemetryAnalyzer();
        var points = new[]
        {
            new TelemetryPoint { Index = 5, SpeedKmh = 42, LeanAngleDeg = 8.0, DistanceFromStartMeters = 100 },
            new TelemetryPoint { Index = 6, SpeedKmh = 46, LeanAngleDeg = 6.0, DistanceFromStartMeters = 132.4 },
            new TelemetryPoint { Index = 7, SpeedKmh = 48, LeanAngleDeg = 4.0, DistanceFromStartMeters = 155.9 }
        };

        var stats = analyzer.Analyze(points);

        Assert.Equal(55.9, stats.TotalDistanceMeters, 3);
    }

    [Fact]
    public void Analyze_UsesTimeWeightedAverageSpeed_WhenSegmentDurationsDiffer()
    {
        var analyzer = new TelemetryAnalyzer();
        var points = new[]
        {
            new TelemetryPoint { Index = 1, SpeedKmh = 10, LeanAngleDeg = 1, DistanceFromStartMeters = 0 },
            new TelemetryPoint { Index = 2, SpeedKmh = 100, LeanAngleDeg = 2, DistanceFromStartMeters = 10 },
            new TelemetryPoint { Index = 3, SpeedKmh = 100, LeanAngleDeg = 3, DistanceFromStartMeters = 1010 }
        };

        var stats = analyzer.Analyze(points);
        double expectedAverageSpeed =
            1.01d /
            ((0.01d / 55d) + (1d / 100d));

        Assert.NotEqual((10d + 100d + 100d) / 3d, stats.AverageSpeedKmh, 3);
        Assert.Equal(expectedAverageSpeed, stats.AverageSpeedKmh, 3);
    }

    [Fact]
    public void Analyze_FallsBackToSimpleAverage_WhenNoSegmentDurationCanBeInferred()
    {
        var analyzer = new TelemetryAnalyzer();
        var points = new[]
        {
            new TelemetryPoint { Index = 1, SpeedKmh = 10, LeanAngleDeg = 1, DistanceFromStartMeters = 100 },
            new TelemetryPoint { Index = 2, SpeedKmh = 30, LeanAngleDeg = 2, DistanceFromStartMeters = 100 }
        };

        var stats = analyzer.Analyze(points);

        Assert.Equal(20, stats.AverageSpeedKmh, 3);
    }
}
