using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.App.Models;

public sealed class TelemetrySeriesSnapshot
{
    private readonly double[] _speedBuffer;
    private readonly double[] _leanBuffer;
    private readonly double[] _accelXBuffer;
    private readonly double[] _accelYBuffer;
    private readonly double[] _accelZBuffer;
    private readonly int _offset;
    private readonly int _count;

    public static TelemetrySeriesSnapshot Empty { get; } = new(
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<double>(),
        offset: 0,
        count: 0);

    private TelemetrySeriesSnapshot(
        double[] speedBuffer,
        double[] leanBuffer,
        double[] accelXBuffer,
        double[] accelYBuffer,
        double[] accelZBuffer,
        int offset,
        int count)
    {
        _speedBuffer = speedBuffer;
        _leanBuffer = leanBuffer;
        _accelXBuffer = accelXBuffer;
        _accelYBuffer = accelYBuffer;
        _accelZBuffer = accelZBuffer;
        _offset = offset;
        _count = count;

        SpeedSeries = CreateView(_speedBuffer, _offset, _count);
        LeanSeries = CreateView(_leanBuffer, _offset, _count);
        AccelXSeries = CreateView(_accelXBuffer, _offset, _count);
        AccelYSeries = CreateView(_accelYBuffer, _offset, _count);
        AccelZSeries = CreateView(_accelZBuffer, _offset, _count);
        AccelSeries =
        [
            new ChartSeriesDefinition("Accel X", AccelXSeries, "#C65D7B"),
            new ChartSeriesDefinition("Accel Y", AccelYSeries, "#E09F3E"),
            new ChartSeriesDefinition("Accel Z", AccelZSeries, "#8F2D3B")
        ];
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

        return new TelemetrySeriesSnapshot(
            speedSeries,
            leanSeries,
            accelXSeries,
            accelYSeries,
            accelZSeries,
            offset: 0,
            count: points.Count);
    }

    public TelemetrySeriesSnapshot Slice(int startOffset, int count)
    {
        if (count <= 0)
            return Empty;

        int normalizedStart = Math.Clamp(startOffset, 0, _count);
        int normalizedCount = Math.Clamp(count, 0, _count - normalizedStart);
        if (normalizedCount == 0)
            return Empty;

        if (normalizedStart == 0 && normalizedCount == _count)
            return this;

        return new TelemetrySeriesSnapshot(
            _speedBuffer,
            _leanBuffer,
            _accelXBuffer,
            _accelYBuffer,
            _accelZBuffer,
            _offset + normalizedStart,
            normalizedCount);
    }

    private static IReadOnlyList<double> CreateView(double[] source, int offset, int count)
    {
        if (count == 0)
            return Array.Empty<double>();

        return offset == 0 && count == source.Length
            ? source
            : new ArraySegment<double>(source, offset, count);
    }
}
