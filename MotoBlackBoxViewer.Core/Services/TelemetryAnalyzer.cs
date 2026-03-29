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
        double speedSum = 0;
        double maxSpeed = double.MinValue;
        double minLean = double.MaxValue;
        double maxLean = double.MinValue;

        for (int i = 0; i < points.Count; i++)
        {
            TelemetryPoint point = points[i];
            speedSum += point.SpeedKmh;

            if (point.SpeedKmh > maxSpeed)
                maxSpeed = point.SpeedKmh;

            if (point.LeanAngleDeg < minLean)
                minLean = point.LeanAngleDeg;

            if (point.LeanAngleDeg > maxLean)
                maxLean = point.LeanAngleDeg;
        }

        return new TelemetryStatistics
        {
            PointCount = points.Count,
            TotalDistanceMeters = totalDistance,
            AverageSpeedKmh = speedSum / points.Count,
            MaxSpeedKmh = maxSpeed,
            MinLeanDeg = minLean,
            MaxLeanDeg = maxLean
        };
    }
}
