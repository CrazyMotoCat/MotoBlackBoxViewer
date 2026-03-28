using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.App.Models;

public sealed class TelemetrySeriesSnapshot
{
    public static TelemetrySeriesSnapshot Empty { get; } = new(
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<ChartSeriesDefinition>());

    private TelemetrySeriesSnapshot(
        IReadOnlyList<double> speedSeries,
        IReadOnlyList<double> leanSeries,
        IReadOnlyList<double> accelXSeries,
        IReadOnlyList<double> accelYSeries,
        IReadOnlyList<double> accelZSeries,
        IReadOnlyList<ChartSeriesDefinition> accelSeries)
    {
        SpeedSeries = speedSeries;
        LeanSeries = leanSeries;
        AccelXSeries = accelXSeries;
        AccelYSeries = accelYSeries;
        AccelZSeries = accelZSeries;
        AccelSeries = accelSeries;
    }

    public IReadOnlyList<double> SpeedSeries { get; }

    public IReadOnlyList<double> LeanSeries { get; }

    public IReadOnlyList<double> AccelXSeries { get; }

    public IReadOnlyList<double> AccelYSeries { get; }

    public IReadOnlyList<double> AccelZSeries { get; }

    public IReadOnlyList<ChartSeriesDefinition> AccelSeries { get; }

    public static TelemetrySeriesSnapshot Create(IReadOnlyList<TelemetryPoint> points)
    {
        if (points.Count == 0)
            return Empty;

        var speedSeries = new double[points.Count];
        var leanSeries = new double[points.Count];
        var accelXSeries = new double[points.Count];
        var accelYSeries = new double[points.Count];
        var accelZSeries = new double[points.Count];

        for (int i = 0; i < points.Count; i++)
        {
            TelemetryPoint point = points[i];
            speedSeries[i] = point.SpeedKmh;
            leanSeries[i] = point.LeanAngleDeg;
            accelXSeries[i] = point.AccelX;
            accelYSeries[i] = point.AccelY;
            accelZSeries[i] = point.AccelZ;
        }

        ChartSeriesDefinition[] accelSeries =
        [
            new ChartSeriesDefinition("Accel X", accelXSeries, "#22C55E"),
            new ChartSeriesDefinition("Accel Y", accelYSeries, "#F59E0B"),
            new ChartSeriesDefinition("Accel Z", accelZSeries, "#EF4444")
        ];

        return new TelemetrySeriesSnapshot(
            speedSeries,
            leanSeries,
            accelXSeries,
            accelYSeries,
            accelZSeries,
            accelSeries);
    }
}
