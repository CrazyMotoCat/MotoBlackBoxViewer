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
        CsvTelemetryReader reader = new();

        MotoBlackBoxViewer.Core.Models.CsvTelemetryReadResult readResult = await reader.ReadAsync(filePath);
        IReadOnlyList<MotoBlackBoxViewer.Core.Models.TelemetryPoint> points = readResult.Points;

        Assert.Equal(2, points.Count);
        Assert.Equal(0, readResult.SkippedRowCount);
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
        CsvTelemetryReader reader = new();

        MotoBlackBoxViewer.Core.Models.CsvTelemetryReadResult readResult = await reader.ReadAsync(filePath);
        IReadOnlyList<MotoBlackBoxViewer.Core.Models.TelemetryPoint> points = readResult.Points;

        Assert.Equal(3, points.Count);
        Assert.Equal(0, readResult.SkippedRowCount);
        Assert.Equal(0, points[0].DistanceFromStartMeters, 6);
        Assert.True(points[1].DistanceFromStartMeters > 0);
        Assert.True(points[2].DistanceFromStartMeters > points[1].DistanceFromStartMeters);
    }

    [Fact]
    public async Task ReadAsync_HandlesLargeStreamingImportWithBlankLines()
    {
        StringBuilder csv = new();
        csv.AppendLine("lat;lon;speed;accelZ;accelX;accelY;lean");

        for (int i = 0; i < 10000; i++)
        {
            double latitude = 43.116877 + (i * 0.00001);
            double longitude = 131.896234 - (i * 0.00001);
            double speed = 12.8 + (i % 7);
            double lean = 1.5 + ((i % 9) * 0.1);

            csv.Append(latitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
            csv.Append(';');
            csv.Append(longitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
            csv.Append(';');
            csv.Append(speed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
            csv.Append(";9.81;0.03;0.02;");
            csv.AppendLine(lean.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));

            if (i % 1000 == 0)
                csv.AppendLine();
        }

        string filePath = CreateTempFile(csv.ToString(), Encoding.UTF8);
        CsvTelemetryReader reader = new();

        MotoBlackBoxViewer.Core.Models.CsvTelemetryReadResult readResult = await reader.ReadAsync(filePath);

        Assert.Equal(10000, readResult.Points.Count);
        Assert.Equal(0, readResult.SkippedRowCount);
        Assert.Empty(readResult.RowIssues);
        Assert.Equal(1, readResult.Points[0].Index);
        Assert.Equal(10000, readResult.Points[^1].Index);
        Assert.True(readResult.Points[^1].DistanceFromStartMeters > readResult.Points[0].DistanceFromStartMeters);
    }

    [Fact]
    public async Task ReadAsync_SupportsEnglishAliases()
    {
        const string csv = "lat;lon;speed;accelZ;accelX;accelY;lean\n" +
                           "43.116877;131.896234;12.8;9.81;0.03;0.02;1.5\n";

        string filePath = CreateTempFile(csv, Encoding.UTF8);
        CsvTelemetryReader reader = new();

        MotoBlackBoxViewer.Core.Models.CsvTelemetryReadResult readResult = await reader.ReadAsync(filePath);
        IReadOnlyList<MotoBlackBoxViewer.Core.Models.TelemetryPoint> points = readResult.Points;

        MotoBlackBoxViewer.Core.Models.TelemetryPoint point = Assert.Single(points);
        Assert.Equal(1, point.Index);
        Assert.Equal(12.8, point.SpeedKmh, 3);
        Assert.Equal(1.5, point.LeanAngleDeg, 3);
    }

    [Fact]
    public async Task ReadAsync_SupportsQuotedFields()
    {
        const string csv = "\"lat\";\"lon\";\"speed\";\"accelZ\";\"accelX\";\"accelY\";\"lean\"\n" +
                           "\"43.116877\";\"131.896234\";\"12,8\";\"9.81\";\"0.03\";\"0.02\";\"1.5\"\n";

        string filePath = CreateTempFile(csv, Encoding.UTF8);
        CsvTelemetryReader reader = new();

        MotoBlackBoxViewer.Core.Models.CsvTelemetryReadResult readResult = await reader.ReadAsync(filePath);
        IReadOnlyList<MotoBlackBoxViewer.Core.Models.TelemetryPoint> points = readResult.Points;

        MotoBlackBoxViewer.Core.Models.TelemetryPoint point = Assert.Single(points);
        Assert.Equal(43.116877, point.Latitude, 6);
        Assert.Equal(131.896234, point.Longitude, 6);
        Assert.Equal(12.8, point.SpeedKmh, 3);
    }

    [Fact]
    public async Task ReadAsync_ThrowsWhenRequiredColumnIsMissing()
    {
        const string csv = "Широта;Долгота;Скорость\n43.1;131.8;10\n";
        string filePath = CreateTempFile(csv, Encoding.UTF8);
        CsvTelemetryReader reader = new();

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadAsync(filePath));

        Assert.Contains("Required column", ex.Message);
    }

    [Fact]
    public async Task ReadAsync_ReportsMalformedRowsAndKeepsValidOnes()
    {
        const string csv = "lat;lon;speed;accelZ;accelX;accelY;lean\n" +
                           "43.116877;131.896234;12.8;9.81;0.03;0.02;1.5\n" +
                           "43.116934;131.895912;broken;9.80;0.02;0.01;1.7\n" +
                           "43.116988;131.895633;14.1\n";

        string filePath = CreateTempFile(csv, Encoding.UTF8);
        CsvTelemetryReader reader = new();

        MotoBlackBoxViewer.Core.Models.CsvTelemetryReadResult readResult = await reader.ReadAsync(filePath);

        Assert.Single(readResult.Points);
        Assert.Equal(2, readResult.SkippedRowCount);
        Assert.Equal(2, readResult.RowIssues.Count);
        Assert.Equal(3, readResult.RowIssues[0].LineNumber);
        Assert.Equal(4, readResult.RowIssues[1].LineNumber);
    }

    [Fact]
    public async Task ReadAsync_WhenAllDataRowsAreMalformed_ThrowsExplicitDiagnostics()
    {
        const string csv = "lat;lon;speed;accelZ;accelX;accelY;lean\n" +
                           "43.116934;131.895912;broken;9.80;0.02;0.01;1.7\n";

        string filePath = CreateTempFile(csv, Encoding.UTF8);
        CsvTelemetryReader reader = new();

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadAsync(filePath));

        Assert.Contains("does not contain valid telemetry rows", ex.Message);
        Assert.Contains("Skipped 1 malformed row", ex.Message);
        Assert.Contains("line 2", ex.Message);
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
        foreach (string file in _tempFiles)
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
