namespace MotoBlackBoxViewer.Core.Models;

public sealed class TelemetryStatistics
{
    public int PointCount { get; set; }
    public double TotalDistanceMeters { get; set; }
    public double AverageSpeedKmh { get; set; }
    public double MaxSpeedKmh { get; set; }
    public double MinLeanDeg { get; set; }
    public double MaxLeanDeg { get; set; }
}
