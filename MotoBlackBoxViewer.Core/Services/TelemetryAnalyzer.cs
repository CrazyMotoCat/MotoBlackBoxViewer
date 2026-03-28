using MotoBlackBoxViewer.Core.Interfaces;
using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.Core.Services;

public sealed class TelemetryAnalyzer : ITelemetryAnalyzer
{
    public TelemetryStatistics Analyze(IReadOnlyList<TelemetryPoint> points)
    {
        if (points.Count == 0)
            return new TelemetryStatistics();

        double startDistance = points[0].DistanceFromStartMeters;
        double endDistance = points[^1].DistanceFromStartMeters;
        double totalDistance = Math.Max(0, endDistance - startDistance);

        return new TelemetryStatistics
        {
            PointCount = points.Count,
            TotalDistanceMeters = totalDistance,
            AverageSpeedKmh = points.Average(p => p.SpeedKmh),
            MaxSpeedKmh = points.Max(p => p.SpeedKmh),
            MinLeanDeg = points.Min(p => p.LeanAngleDeg),
            MaxLeanDeg = points.Max(p => p.LeanAngleDeg)
        };
    }
}
