using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.App.Controls;
using MotoBlackBoxViewer.Core.Interfaces;
using MotoBlackBoxViewer.Core.Models;
using System.Diagnostics;

namespace MotoBlackBoxViewer.App.Services;

public sealed class TelemetryDataProcessor
{
    private readonly ITelemetryAnalyzer _analyzer;
    private IReadOnlyList<TelemetryPoint>? _cachedAllPoints;
    private int _cachedPointCount;
    private TelemetryPoint? _cachedFirstPoint;
    private TelemetryPoint? _cachedLastPoint;
    private TelemetrySeriesSnapshot _cachedFullSeriesSnapshot = TelemetrySeriesSnapshot.Empty;

    public TelemetryDataProcessor(ITelemetryAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    public TelemetryVisibleData CreateVisibleData(IReadOnlyList<TelemetryPoint> allPoints, int startIndex, int endIndex)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        if (allPoints.Count == 0)
            return TelemetryVisibleData.Empty;

        int normalizedStart = Math.Clamp(startIndex, 1, allPoints.Count);
        int normalizedEnd = Math.Clamp(endIndex, normalizedStart, allPoints.Count);
        int startOffset = normalizedStart - 1;
        int count = normalizedEnd - normalizedStart + 1;
        List<TelemetryPoint> filtered = SlicePoints(allPoints, startOffset, count);
        (TelemetrySeriesSnapshot sourceSeriesSnapshot, bool usedCachedFullSeries) = GetOrCreateFullSeriesSnapshot(allPoints);

        IReadOnlyDictionary<int, int> visiblePositionsByPointIndex =
            filtered.Count == 0
                ? new Dictionary<int, int>()
                : new SequentialVisiblePositionMap(filtered[0].Index, filtered.Count);

        TelemetryVisibleData visibleData = new(
            filtered,
            _analyzer.Analyze(filtered),
            sourceSeriesSnapshot.Slice(startOffset, count),
            visiblePositionsByPointIndex);

        stopwatch.Stop();
        ChartPerformanceDiagnostics.Report(
            operation: "CreateVisibleData",
            inputPointCount: allPoints.Count,
            outputPointCount: filtered.Count,
            elapsed: stopwatch.Elapsed,
            detail: $"range={normalizedStart}-{normalizedEnd}; cachedFullSeries={usedCachedFullSeries}");
        return visibleData;
    }

    private (TelemetrySeriesSnapshot Snapshot, bool UsedCached) GetOrCreateFullSeriesSnapshot(IReadOnlyList<TelemetryPoint> allPoints)
    {
        TelemetryPoint? firstPoint = allPoints.Count > 0 ? allPoints[0] : null;
        TelemetryPoint? lastPoint = allPoints.Count > 0 ? allPoints[^1] : null;

        if (ReferenceEquals(_cachedAllPoints, allPoints)
            && _cachedPointCount == allPoints.Count
            && ReferenceEquals(_cachedFirstPoint, firstPoint)
            && ReferenceEquals(_cachedLastPoint, lastPoint))
        {
            return (_cachedFullSeriesSnapshot, true);
        }

        _cachedAllPoints = allPoints;
        _cachedPointCount = allPoints.Count;
        _cachedFirstPoint = firstPoint;
        _cachedLastPoint = lastPoint;
        _cachedFullSeriesSnapshot = TelemetrySeriesSnapshot.Create(allPoints);
        return (_cachedFullSeriesSnapshot, false);
    }

    private static List<TelemetryPoint> SlicePoints(IReadOnlyList<TelemetryPoint> allPoints, int startOffset, int count)
    {
        if (count <= 0)
            return [];

        if (allPoints is List<TelemetryPoint> list)
            return list.GetRange(startOffset, count);

        var result = new List<TelemetryPoint>(count);
        for (int i = 0; i < count; i++)
            result.Add(allPoints[startOffset + i]);

        return result;
    }
}
