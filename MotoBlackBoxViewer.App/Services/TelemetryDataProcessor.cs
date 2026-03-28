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

        var filtered = allPoints
            .Where(p => p.Index >= startIndex && p.Index <= endIndex)
            .ToList();

        var visiblePositionsByPointIndex = new Dictionary<int, int>(filtered.Count);
        for (int i = 0; i < filtered.Count; i++)
            visiblePositionsByPointIndex[filtered[i].Index] = i + 1;

        return new TelemetryVisibleData(
            filtered,
            _analyzer.Analyze(filtered),
            TelemetrySeriesSnapshot.Create(filtered),
            visiblePositionsByPointIndex);
    }
}
