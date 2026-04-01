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
        double simpleSpeedSum = 0;
        double weightedDistanceKm = 0;
        double weightedDurationHours = 0;
        double maxSpeed = double.MinValue;
        double minLean = double.MaxValue;
        double maxLean = double.MinValue;

        for (int i = 0; i < points.Count; i++)
        {
            TelemetryPoint point = points[i];
            simpleSpeedSum += point.SpeedKmh;

            if (point.SpeedKmh > maxSpeed)
                maxSpeed = point.SpeedKmh;

            if (point.LeanAngleDeg < minLean)
                minLean = point.LeanAngleDeg;

            if (point.LeanAngleDeg > maxLean)
                maxLean = point.LeanAngleDeg;

            if (i == 0)
                continue;

            double segmentDistanceMeters = Math.Max(0, point.DistanceFromStartMeters - points[i - 1].DistanceFromStartMeters);
            if (segmentDistanceMeters <= 0)
                continue;

            // We do not have explicit timestamps, so infer segment time from distance and the midpoint speed.
            double segmentSpeedKmh = GetSegmentSpeedKmh(points[i - 1], point);
            if (segmentSpeedKmh <= 0)
                continue;

            double segmentDistanceKm = segmentDistanceMeters / 1000d;
            weightedDistanceKm += segmentDistanceKm;
            weightedDurationHours += segmentDistanceKm / segmentSpeedKmh;
        }

        double averageSpeedKmh =
            weightedDurationHours > 0
                ? weightedDistanceKm / weightedDurationHours
                : simpleSpeedSum / points.Count;

        return new TelemetryStatistics
        {
            PointCount = points.Count,
            TotalDistanceMeters = totalDistance,
            AverageSpeedKmh = averageSpeedKmh,
            MaxSpeedKmh = maxSpeed,
            MinLeanDeg = minLean,
            MaxLeanDeg = maxLean
        };
    }

    private static double GetSegmentSpeedKmh(TelemetryPoint start, TelemetryPoint end)
    {
        return (start.SpeedKmh + end.SpeedKmh) / 2d;
    }
}
