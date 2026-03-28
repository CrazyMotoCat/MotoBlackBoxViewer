using System.Text;
using MotoBlackBoxViewer.Core.Services;

namespace MotoBlackBoxViewer.Tests;

public sealed class CsvTelemetryReaderTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    [Fact]
    public async Task ReadAsync_ParsesRussianHeaders_WithDecimalCommas()
    {
        const string csv = "Широта;Долгота;Скорость;Ускорение по Z;Ускорение по X;Ускорение по Y;Угол наклона\n" +
                           "43,116877;131,896234;12,8;9,81;0,03;0,02;1,5\n" +
                           "43,116934;131,895912;13,4;9,80;0,02;0,01;1,7\n";

        string filePath = CreateTempFile(csv, Encoding.UTF8);
        var reader = new CsvTelemetryReader();

        var points = await reader.ReadAsync(filePath);

        Assert.Equal(2, points.Count);
        Assert.Equal(43.116877, points[0].Latitude, 6);
        Assert.Equal(131.896234, points[0].Longitude, 6);
        Assert.Equal(12.8, points[0].SpeedKmh, 3);
        Assert.Equal(9.81, points[0].AccelZ, 3);
        Assert.Equal(0.03, points[0].AccelX, 3);
        Assert.Equal(0.02, points[0].AccelY, 3);
        Assert.Equal(1.5, points[0].LeanAngleDeg, 3);
    }

    [Fact]
    public async Task ReadAsync_ParsesCp1251File_AndCalculatesDistances()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        const string csv = "Широта;Долгота;Скорость;Ускорение по Z;Ускорение по X;Ускорение по Y;Угол наклона\n" +
                           "43.116877;131.896234;12.8;9.81;0.03;0.02;1.5\n" +
                           "43.116934;131.895912;13.4;9.80;0.02;0.01;1.7\n" +
                           "43.116988;131.895633;14.1;9.79;0.02;0.01;1.9\n";

        string filePath = CreateTempFile(csv, Encoding.GetEncoding(1251));
        var reader = new CsvTelemetryReader();

        var points = await reader.ReadAsync(filePath);

        Assert.Equal(3, points.Count);
        Assert.Equal(0, points[0].DistanceFromStartMeters, 6);
        Assert.True(points[1].DistanceFromStartMeters > 0);
        Assert.True(points[2].DistanceFromStartMeters > points[1].DistanceFromStartMeters);
    }

    [Fact]
    public async Task ReadAsync_SupportsEnglishAliases()
    {
        const string csv = "lat;lon;speed;accelZ;accelX;accelY;lean\n" +
                           "43.116877;131.896234;12.8;9.81;0.03;0.02;1.5\n";

        string filePath = CreateTempFile(csv, Encoding.UTF8);
        var reader = new CsvTelemetryReader();

        var points = await reader.ReadAsync(filePath);

        var point = Assert.Single(points);
        Assert.Equal(1, point.Index);
        Assert.Equal(12.8, point.SpeedKmh, 3);
        Assert.Equal(1.5, point.LeanAngleDeg, 3);
    }

    [Fact]
    public async Task ReadAsync_ThrowsWhenRequiredColumnIsMissing()
    {
        const string csv = "Широта;Долгота;Скорость\n43.1;131.8;10\n";
        string filePath = CreateTempFile(csv, Encoding.UTF8);
        var reader = new CsvTelemetryReader();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadAsync(filePath));

        Assert.Contains("Не найдена колонка", ex.Message);
    }

    private string CreateTempFile(string content, Encoding encoding)
    {
        string path = Path.Combine(Path.GetTempPath(), $"motobbv_tests_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content, encoding);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch
            {
                // Ignore cleanup failures in tests.
            }
        }
    }
}
