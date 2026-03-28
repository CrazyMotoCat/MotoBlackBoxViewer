using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.App.Models;

public sealed class TelemetryVisibleData
{
    public static TelemetryVisibleData Empty { get; } = new(
        Array.Empty<TelemetryPoint>(),
        new TelemetryStatistics(),
        TelemetrySeriesSnapshot.Empty,
        new Dictionary<int, int>());

    public TelemetryVisibleData(
        IReadOnlyList<TelemetryPoint> points,
        TelemetryStatistics statistics,
        TelemetrySeriesSnapshot seriesSnapshot,
        IReadOnlyDictionary<int, int> visiblePositionsByPointIndex)
    {
        Points = points;
        Statistics = statistics;
        SeriesSnapshot = seriesSnapshot;
        VisiblePositionsByPointIndex = visiblePositionsByPointIndex;
    }

    public IReadOnlyList<TelemetryPoint> Points { get; }

    public TelemetryStatistics Statistics { get; }

    public TelemetrySeriesSnapshot SeriesSnapshot { get; }

    public IReadOnlyDictionary<int, int> VisiblePositionsByPointIndex { get; }
}
