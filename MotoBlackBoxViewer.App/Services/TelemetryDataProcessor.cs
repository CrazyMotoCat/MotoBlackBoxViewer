using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.Core.Interfaces;
using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.App.Services;

internal sealed class TelemetryDataProcessor
{
    private readonly ITelemetryAnalyzer _analyzer;

    public TelemetryDataProcessor(ITelemetryAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    public TelemetryVisibleData CreateVisibleData(IReadOnlyList<TelemetryPoint> allPoints, int startIndex, int endIndex)
    {
        if (allPoints.Count == 0)
            return TelemetryVisibleData.Empty;

        int normalizedStart = Math.Clamp(startIndex, 1, allPoints.Count);
        int normalizedEnd = Math.Clamp(endIndex, normalizedStart, allPoints.Count);
        int startOffset = normalizedStart - 1;
        int count = normalizedEnd - normalizedStart + 1;
        List<TelemetryPoint> filtered = SlicePoints(allPoints, startOffset, count);

        var visiblePositionsByPointIndex = new Dictionary<int, int>(filtered.Count);
        for (int i = 0; i < filtered.Count; i++)
            visiblePositionsByPointIndex[filtered[i].Index] = i + 1;

        return new TelemetryVisibleData(
            filtered,
            _analyzer.Analyze(filtered),
            TelemetrySeriesSnapshot.Create(filtered),
            visiblePositionsByPointIndex);
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
