namespace MotoBlackBoxViewer.Core.Models;

public sealed class TelemetryPoint
{
    public int Index { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double SpeedKmh { get; set; }
    public double AccelZ { get; set; }
    public double AccelX { get; set; }
    public double AccelY { get; set; }
    public double LeanAngleDeg { get; set; }
    public double DistanceFromStartMeters { get; set; }

    public override string ToString()
        => $"#{Index}: {Latitude:F6}, {Longitude:F6}, {SpeedKmh:F1} km/h";
}
