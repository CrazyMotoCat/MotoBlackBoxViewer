using System.Globalization;
using System.Text;
using MotoBlackBoxViewer.Core.Interfaces;
using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.Core.Services;

public sealed class CsvTelemetryReader : ICsvTelemetryReader
{
    public async Task<IReadOnlyList<TelemetryPoint>> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        string[] lines;
        try
        {
            lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8, cancellationToken);
            if (lines.Length > 0 && lines[0].Contains('�'))
                throw new InvalidOperationException("UTF-8 fallback needed.");
        }
        catch
        {
            lines = await File.ReadAllLinesAsync(filePath, Encoding.GetEncoding(1251), cancellationToken);
        }

        if (lines.Length < 2)
            throw new InvalidOperationException("CSV пустой или не содержит данных.");

        var headers = lines[0].Split(';').Select(Normalize).ToArray();

        int latIndex = FindColumn(headers, "широта", "latitude", "lat");
        int lonIndex = FindColumn(headers, "долгота", "longitude", "lon", "lng");
        int speedIndex = FindColumn(headers, "скорость", "speed", "speedkmh");
        int accelZIndex = FindColumn(headers, "ускорениепoz", "accelz", "az");
        int accelXIndex = FindColumn(headers, "ускорениеpox", "accelx", "ax");
        int accelYIndex = FindColumn(headers, "ускорениеpoy", "accely", "ay");
        int leanIndex = FindColumn(headers, "уголнаклона", "lean", "leanangle", "roll");

        var result = new List<TelemetryPoint>();

        for (int i = 1; i < lines.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var parts = lines[i].Split(';');
            if (parts.Length < headers.Length)
                continue;

            var point = new TelemetryPoint
            {
                Index = result.Count + 1,
                Latitude = ParseDouble(parts[latIndex]),
                Longitude = ParseDouble(parts[lonIndex]),
                SpeedKmh = ParseDouble(parts[speedIndex]),
                AccelZ = ParseDouble(parts[accelZIndex]),
                AccelX = ParseDouble(parts[accelXIndex]),
                AccelY = ParseDouble(parts[accelYIndex]),
                LeanAngleDeg = ParseDouble(parts[leanIndex])
            };

            result.Add(point);
        }

        FillDistances(result);
        return result;
    }

    private static string Normalize(string value)
    {
        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(ch => !char.IsWhiteSpace(ch) && ch != '_' && ch != '-')
            .ToArray());
    }

    private static int FindColumn(string[] headers, params string[] aliases)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            if (aliases.Contains(headers[i], StringComparer.OrdinalIgnoreCase))
                return i;
        }

        throw new InvalidOperationException($"Не найдена колонка. Ищу один из вариантов: {string.Join(", ", aliases)}");
    }

    private static double ParseDouble(string value)
    {
        value = value.Trim().Replace(',', '.');

        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        throw new FormatException($"Не удалось распарсить число: '{value}'");
    }

    private static void FillDistances(IReadOnlyList<TelemetryPoint> points)
    {
        double total = 0;

        for (int i = 0; i < points.Count; i++)
        {
            if (i > 0)
            {
                total += HaversineMeters(
                    points[i - 1].Latitude,
                    points[i - 1].Longitude,
                    points[i].Latitude,
                    points[i].Longitude);
            }

            points[i].DistanceFromStartMeters = total;
        }
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadius = 6371000d;

        double dLat = DegreesToRadians(lat2 - lat1);
        double dLon = DegreesToRadians(lon2 - lon1);

        double a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadius * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}
