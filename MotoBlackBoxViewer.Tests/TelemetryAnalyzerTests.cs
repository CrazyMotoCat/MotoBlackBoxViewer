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
        Assert.Equal(0, stats.PeakLeanAbsDeg);
        Assert.Equal(0, stats.HardBrakingEventCount);
        Assert.Equal(0, stats.SharpAccelerationEventCount);
        Assert.Equal(0, stats.PeakLeanEventCount);
        Assert.Equal(0, stats.StopEventCount);
        Assert.Equal(0, stats.StartEventCount);
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
        Assert.Equal(3.0, stats.PeakLeanAbsDeg, 3);
        Assert.Equal(0, stats.HardBrakingEventCount);
        Assert.Equal(0, stats.SharpAccelerationEventCount);
        Assert.Equal(0, stats.PeakLeanEventCount);
        Assert.Equal(0, stats.StopEventCount);
        Assert.Equal(0, stats.StartEventCount);
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

    [Fact]
    public void Analyze_FiltersSingleGpsSpikeThatReturnsToRoute()
    {
        var analyzer = new TelemetryAnalyzer();
        var points = new[]
        {
            new TelemetryPoint { Index = 1, Latitude = 43.116877, Longitude = 131.896234, SpeedKmh = 40, LeanAngleDeg = 1, DistanceFromStartMeters = 0 },
            new TelemetryPoint { Index = 2, Latitude = 44.116877, Longitude = 132.896234, SpeedKmh = 42, LeanAngleDeg = 2, DistanceFromStartMeters = 250000 },
            new TelemetryPoint { Index = 3, Latitude = 43.116980, Longitude = 131.896320, SpeedKmh = 41, LeanAngleDeg = 3, DistanceFromStartMeters = 500000 }
        };

        var stats = analyzer.Analyze(points);

        Assert.InRange(stats.TotalDistanceMeters, 0, 20);
        Assert.Equal(42, stats.MaxSpeedKmh, 3);
        Assert.Equal(1, stats.MinLeanDeg, 3);
        Assert.Equal(3, stats.MaxLeanDeg, 3);
    }

    [Fact]
    public void Analyze_DoesNotFilterLegitimateForwardMovement()
    {
        var analyzer = new TelemetryAnalyzer();
        var points = new[]
        {
            new TelemetryPoint { Index = 1, Latitude = 43.116877, Longitude = 131.896234, SpeedKmh = 30, LeanAngleDeg = 1, DistanceFromStartMeters = 0 },
            new TelemetryPoint { Index = 2, Latitude = 43.117377, Longitude = 131.896734, SpeedKmh = 35, LeanAngleDeg = 2, DistanceFromStartMeters = 100 },
            new TelemetryPoint { Index = 3, Latitude = 43.117877, Longitude = 131.897234, SpeedKmh = 40, LeanAngleDeg = 3, DistanceFromStartMeters = 200 }
        };

        var stats = analyzer.Analyze(points);

        Assert.True(stats.TotalDistanceMeters > 100);
        Assert.True(stats.AverageSpeedKmh > 0);
    }

    [Fact]
    public void Analyze_CountsHardBrakingAndSharpAccelerationEvents_ByTransition()
    {
        var analyzer = new TelemetryAnalyzer();
        var points = new[]
        {
            new TelemetryPoint { Index = 1, SpeedKmh = 40, AccelX = 0.0, LeanAngleDeg = 1, DistanceFromStartMeters = 0 },
            new TelemetryPoint { Index = 2, SpeedKmh = 38, AccelX = -0.5, LeanAngleDeg = 2, DistanceFromStartMeters = 20 },
            new TelemetryPoint { Index = 3, SpeedKmh = 35, AccelX = -0.6, LeanAngleDeg = 3, DistanceFromStartMeters = 35 },
            new TelemetryPoint { Index = 4, SpeedKmh = 36, AccelX = 0.0, LeanAngleDeg = 1, DistanceFromStartMeters = 50 },
            new TelemetryPoint { Index = 5, SpeedKmh = 42, AccelX = 0.5, LeanAngleDeg = 2, DistanceFromStartMeters = 70 },
            new TelemetryPoint { Index = 6, SpeedKmh = 48, AccelX = 0.6, LeanAngleDeg = 3, DistanceFromStartMeters = 100 },
            new TelemetryPoint { Index = 7, SpeedKmh = 44, AccelX = -0.4, LeanAngleDeg = 1, DistanceFromStartMeters = 130 }
        };

        var stats = analyzer.Analyze(points);

        Assert.Equal(2, stats.HardBrakingEventCount);
        Assert.Equal(1, stats.SharpAccelerationEventCount);
    }

    [Fact]
    public void Analyze_TracksPeakLeanMagnitudeAndEvents_ByTransition()
    {
        var analyzer = new TelemetryAnalyzer();
        var points = new[]
        {
            new TelemetryPoint { Index = 1, SpeedKmh = 40, LeanAngleDeg = 5, DistanceFromStartMeters = 0 },
            new TelemetryPoint { Index = 2, SpeedKmh = 42, LeanAngleDeg = 31, DistanceFromStartMeters = 25 },
            new TelemetryPoint { Index = 3, SpeedKmh = 44, LeanAngleDeg = 36, DistanceFromStartMeters = 50 },
            new TelemetryPoint { Index = 4, SpeedKmh = 43, LeanAngleDeg = 18, DistanceFromStartMeters = 75 },
            new TelemetryPoint { Index = 5, SpeedKmh = 45, LeanAngleDeg = -34, DistanceFromStartMeters = 100 },
            new TelemetryPoint { Index = 6, SpeedKmh = 46, LeanAngleDeg = -12, DistanceFromStartMeters = 125 }
        };

        var stats = analyzer.Analyze(points);

        Assert.Equal(36, stats.PeakLeanAbsDeg, 3);
        Assert.Equal(2, stats.PeakLeanEventCount);
        Assert.Equal(-34, stats.MinLeanDeg, 3);
        Assert.Equal(36, stats.MaxLeanDeg, 3);
    }

    [Fact]
    public void Analyze_CountsStopAndStartEvents_WithSpeedHysteresis()
    {
        var analyzer = new TelemetryAnalyzer();
        var points = new[]
        {
            new TelemetryPoint { Index = 1, SpeedKmh = 35, DistanceFromStartMeters = 0 },
            new TelemetryPoint { Index = 2, SpeedKmh = 1.5, DistanceFromStartMeters = 10 },
            new TelemetryPoint { Index = 3, SpeedKmh = 0.5, DistanceFromStartMeters = 10 },
            new TelemetryPoint { Index = 4, SpeedKmh = 3.5, DistanceFromStartMeters = 11 },
            new TelemetryPoint { Index = 5, SpeedKmh = 6.0, DistanceFromStartMeters = 14 },
            new TelemetryPoint { Index = 6, SpeedKmh = 1.0, DistanceFromStartMeters = 15 },
            new TelemetryPoint { Index = 7, SpeedKmh = 5.5, DistanceFromStartMeters = 18 }
        };

        var stats = analyzer.Analyze(points);

        Assert.Equal(2, stats.StopEventCount);
        Assert.Equal(2, stats.StartEventCount);
    }
}
