using MotoBlackBoxViewer.Core.Interfaces;
using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.Core.Services;

public sealed class TelemetryAnalyzer : ITelemetryAnalyzer
{
    private const double EarthRadiusMeters = 6371000d;
    private const double GpsOutlierMinimumJumpMeters = 150d;
    private const double GpsOutlierReturnDistanceMeters = 35d;
    private const double GpsOutlierCompressionRatio = 0.2d;
    private const double GpsOutlierSpikeMultiplier = 4d;
    private const double HardBrakingAccelThreshold = -0.35d;
    private const double SharpAccelerationAccelThreshold = 0.35d;
    private const double PeakLeanThresholdDeg = 30d;
    private const double StopSpeedThresholdKmh = 2d;
    private const double StartSpeedThresholdKmh = 5d;

    public TelemetryStatistics Analyze(IReadOnlyList<TelemetryPoint> points)
    {
        if (points.Count == 0)
            return new TelemetryStatistics();

        double simpleSpeedSum = 0;
        double maxSpeed = double.MinValue;
        double minLean = double.MaxValue;
        double maxLean = double.MinValue;
        double peakLeanAbs = 0;
        int hardBrakingEventCount = 0;
        int sharpAccelerationEventCount = 0;
        int peakLeanEventCount = 0;
        int stopEventCount = 0;
        int startEventCount = 0;
        bool isInsideHardBrakingEvent = false;
        bool isInsideSharpAccelerationEvent = false;
        bool isInsidePeakLeanEvent = false;
        bool isInsideStopState = points[0].SpeedKmh <= StopSpeedThresholdKmh;

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

            double absoluteLean = Math.Abs(point.LeanAngleDeg);
            if (absoluteLean > peakLeanAbs)
                peakLeanAbs = absoluteLean;

            UpdateEventCounter(
                point.AccelX <= HardBrakingAccelThreshold,
                ref isInsideHardBrakingEvent,
                ref hardBrakingEventCount);

            UpdateEventCounter(
                point.AccelX >= SharpAccelerationAccelThreshold,
                ref isInsideSharpAccelerationEvent,
                ref sharpAccelerationEventCount);

            UpdateEventCounter(
                absoluteLean >= PeakLeanThresholdDeg,
                ref isInsidePeakLeanEvent,
                ref peakLeanEventCount);

            if (i > 0)
            {
                UpdateStopStartCounters(
                    point.SpeedKmh,
                    ref isInsideStopState,
                    ref stopEventCount,
                    ref startEventCount);
            }
        }

        DistanceAggregation aggregation = HasUsableCoordinates(points) && ContainsGpsOutlierSpike(points)
            ? AggregateFilteredCoordinateDistance(points)
            : AggregateRawDistance(points);

        double averageSpeedKmh =
            aggregation.WeightedDurationHours > 0
                ? aggregation.WeightedDistanceKm / aggregation.WeightedDurationHours
                : simpleSpeedSum / points.Count;

        return new TelemetryStatistics
        {
            PointCount = points.Count,
            TotalDistanceMeters = aggregation.TotalDistanceMeters,
            AverageSpeedKmh = averageSpeedKmh,
            MaxSpeedKmh = maxSpeed,
            MinLeanDeg = minLean,
            MaxLeanDeg = maxLean,
            PeakLeanAbsDeg = peakLeanAbs,
            HardBrakingEventCount = hardBrakingEventCount,
            SharpAccelerationEventCount = sharpAccelerationEventCount,
            PeakLeanEventCount = peakLeanEventCount,
            StopEventCount = stopEventCount,
            StartEventCount = startEventCount
        };
    }

    private static double GetSegmentSpeedKmh(TelemetryPoint start, TelemetryPoint end)
    {
        return (start.SpeedKmh + end.SpeedKmh) / 2d;
    }

    private static DistanceAggregation AggregateRawDistance(IReadOnlyList<TelemetryPoint> points)
    {
        double startDistance = points[0].DistanceFromStartMeters;
        double endDistance = points[^1].DistanceFromStartMeters;
        double totalDistanceMeters = Math.Max(0, endDistance - startDistance);
        double weightedDistanceKm = 0;
        double weightedDurationHours = 0;

        for (int i = 1; i < points.Count; i++)
        {
            AddWeightedSegment(
                points[i - 1],
                points[i],
                Math.Max(0, points[i].DistanceFromStartMeters - points[i - 1].DistanceFromStartMeters),
                ref weightedDistanceKm,
                ref weightedDurationHours);
        }

        return new DistanceAggregation(totalDistanceMeters, weightedDistanceKm, weightedDurationHours);
    }

    private static DistanceAggregation AggregateFilteredCoordinateDistance(IReadOnlyList<TelemetryPoint> points)
    {
        double totalDistanceMeters = 0;
        double weightedDistanceKm = 0;
        double weightedDurationHours = 0;
        int anchorIndex = 0;
        int candidateIndex = 1;

        while (candidateIndex < points.Count)
        {
            int acceptedIndex = candidateIndex;

            if (candidateIndex + 1 < points.Count
                && IsGpsOutlierSpike(points[anchorIndex], points[candidateIndex], points[candidateIndex + 1]))
            {
                acceptedIndex = candidateIndex + 1;
            }

            double segmentDistanceMeters = HaversineMeters(points[anchorIndex], points[acceptedIndex]);
            totalDistanceMeters += segmentDistanceMeters;
            AddWeightedSegment(
                points[anchorIndex],
                points[acceptedIndex],
                segmentDistanceMeters,
                ref weightedDistanceKm,
                ref weightedDurationHours);

            anchorIndex = acceptedIndex;
            candidateIndex = acceptedIndex + 1;
        }

        return new DistanceAggregation(totalDistanceMeters, weightedDistanceKm, weightedDurationHours);
    }

    private static void AddWeightedSegment(
        TelemetryPoint start,
        TelemetryPoint end,
        double segmentDistanceMeters,
        ref double weightedDistanceKm,
        ref double weightedDurationHours)
    {
        if (segmentDistanceMeters <= 0)
            return;

        double segmentSpeedKmh = GetSegmentSpeedKmh(start, end);
        if (segmentSpeedKmh <= 0)
            return;

        double segmentDistanceKm = segmentDistanceMeters / 1000d;
        weightedDistanceKm += segmentDistanceKm;
        weightedDurationHours += segmentDistanceKm / segmentSpeedKmh;
    }

    private static void UpdateEventCounter(bool isTriggered, ref bool isInsideEvent, ref int eventCount)
    {
        if (isTriggered)
        {
            if (!isInsideEvent)
            {
                eventCount++;
                isInsideEvent = true;
            }

            return;
        }

        isInsideEvent = false;
    }

    private static void UpdateStopStartCounters(
        double speedKmh,
        ref bool isInsideStopState,
        ref int stopEventCount,
        ref int startEventCount)
    {
        if (!isInsideStopState && speedKmh <= StopSpeedThresholdKmh)
        {
            stopEventCount++;
            isInsideStopState = true;
            return;
        }

        if (isInsideStopState && speedKmh >= StartSpeedThresholdKmh)
        {
            startEventCount++;
            isInsideStopState = false;
        }
    }

    private static bool HasUsableCoordinates(IReadOnlyList<TelemetryPoint> points)
    {
        for (int i = 0; i < points.Count; i++)
        {
            if (Math.Abs(points[i].Latitude) > double.Epsilon || Math.Abs(points[i].Longitude) > double.Epsilon)
                return true;
        }

        return false;
    }

    private static bool ContainsGpsOutlierSpike(IReadOnlyList<TelemetryPoint> points)
    {
        for (int i = 1; i < points.Count - 1; i++)
        {
            if (IsGpsOutlierSpike(points[i - 1], points[i], points[i + 1]))
                return true;
        }

        return false;
    }

    private static bool IsGpsOutlierSpike(TelemetryPoint start, TelemetryPoint candidate, TelemetryPoint next)
    {
        double firstJumpMeters = HaversineMeters(start, candidate);
        double secondJumpMeters = HaversineMeters(candidate, next);
        double directDistanceMeters = HaversineMeters(start, next);

        if (firstJumpMeters < GpsOutlierMinimumJumpMeters || secondJumpMeters < GpsOutlierMinimumJumpMeters)
            return false;

        double maxAllowedReturnMeters = Math.Max(GpsOutlierReturnDistanceMeters, Math.Min(firstJumpMeters, secondJumpMeters) * GpsOutlierCompressionRatio);
        if (directDistanceMeters > maxAllowedReturnMeters)
            return false;

        return firstJumpMeters + secondJumpMeters >= directDistanceMeters * GpsOutlierSpikeMultiplier;
    }

    private static double HaversineMeters(TelemetryPoint start, TelemetryPoint end)
        => HaversineMeters(start.Latitude, start.Longitude, end.Latitude, end.Longitude);

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = DegreesToRadians(lat2 - lat1);
        double dLon = DegreesToRadians(lon2 - lon1);

        double a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;

    private readonly record struct DistanceAggregation(
        double TotalDistanceMeters,
        double WeightedDistanceKm,
        double WeightedDurationHours);
}
