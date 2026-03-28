using System.Globalization;
using System.Text;
using MotoBlackBoxViewer.Core.Interfaces;
using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.Core.Services;

public sealed class CsvTelemetryReader : ICsvTelemetryReader
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public async Task<IReadOnlyList<TelemetryPoint>> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        string[] lines = await ReadAllLinesWithEncodingFallbackAsync(filePath, cancellationToken);
        if (lines.Length < 2)
            throw new InvalidOperationException("CSV file is empty or does not contain telemetry rows.");

        string[] headers = ParseCsvLine(lines[0]).Select(Normalize).ToArray();

        int latIndex = FindColumn(headers, "широта", "latitude", "lat");
        int lonIndex = FindColumn(headers, "долгота", "longitude", "lon", "lng");
        int speedIndex = FindColumn(headers, "скорость", "speed", "speedkmh");
        int accelZIndex = FindColumn(headers, "ускорениепоz", "accelz", "az");
        int accelXIndex = FindColumn(headers, "ускорениепоx", "ускорениеpox", "accelx", "ax");
        int accelYIndex = FindColumn(headers, "ускорениепоу", "ускорениепоy", "ускорениеpoy", "accely", "ay");
        int leanIndex = FindColumn(headers, "уголнаклона", "lean", "leanangle", "roll");

        List<TelemetryPoint> result = new();

        for (int i = 1; i < lines.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            string[] parts = ParseCsvLine(lines[i]);
            if (parts.Length < headers.Length)
                continue;

            TelemetryPoint point = new()
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

    private static async Task<string[]> ReadAllLinesWithEncodingFallbackAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllLinesAsync(filePath, StrictUtf8, cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            return await File.ReadAllLinesAsync(filePath, Encoding.GetEncoding(1251), cancellationToken);
        }
    }

    private static string[] ParseCsvLine(string line)
    {
        List<string> fields = new();
        StringBuilder current = new();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ';' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString());
        return fields.ToArray();
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

        throw new InvalidOperationException($"Required column was not found. Expected one of: {string.Join(", ", aliases)}");
    }

    private static double ParseDouble(string value)
    {
        value = value.Trim().Replace(',', '.');

        if (double.TryParse(
            value,
            NumberStyles.Float | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out double result))
        {
            return result;
        }

        throw new FormatException($"Unable to parse numeric value: '{value}'");
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
