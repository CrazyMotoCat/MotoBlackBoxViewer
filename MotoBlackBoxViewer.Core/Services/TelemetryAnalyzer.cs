using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.Core.Services;

public sealed class TelemetryAnalyzer
{
    public TelemetryStatistics Analyze(IReadOnlyList<TelemetryPoint> points)
    {
        if (points.Count == 0)
            return new TelemetryStatistics();

        return new TelemetryStatistics
        {
            PointCount = points.Count,
            TotalDistanceMeters = points[^1].DistanceFromStartMeters,
            AverageSpeedKmh = points.Average(p => p.SpeedKmh),
            MaxSpeedKmh = points.Max(p => p.SpeedKmh),
            MinLeanDeg = points.Min(p => p.LeanAngleDeg),
            MaxLeanDeg = points.Max(p => p.LeanAngleDeg)
        };
    }
}
