using System.Globalization;
using System.Text;
using MotoBlackBoxViewer.Core.Interfaces;
using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.Core.Services;

public sealed class CsvTelemetryReader : ICsvTelemetryReader
{
    private const int MaxReportedRowIssues = 10;
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public async Task<CsvTelemetryReadResult> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        try
        {
            return await ReadWithEncodingAsync(filePath, StrictUtf8, cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            return await ReadWithEncodingAsync(filePath, Encoding.GetEncoding(1251), cancellationToken);
        }
    }

    private static async Task<CsvTelemetryReadResult> ReadWithEncodingAsync(
        string filePath,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);
        using StreamReader reader = new(stream, encoding, detectEncodingFromByteOrderMarks: true);

        string? headerLine = await reader.ReadLineAsync().WaitAsync(cancellationToken);
        if (headerLine is null)
            throw new InvalidOperationException("CSV file is empty or does not contain telemetry rows.");

        string[] headers = ParseCsvLine(headerLine).Select(Normalize).ToArray();
        ColumnMap columnMap = CreateColumnMap(headers);

        List<TelemetryPoint> result = new();
        List<CsvTelemetryRowIssue> rowIssues = new();
        int skippedRowCount = 0;
        int lineNumber = 1;
        bool sawAnyDataRow = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? line = await reader.ReadLineAsync().WaitAsync(cancellationToken);
            if (line is null)
                break;

            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            sawAnyDataRow = true;
            string[] parts = ParseCsvLine(line);
            if (parts.Length < headers.Length)
            {
                RegisterRowIssue(rowIssues, lineNumber, "Not enough columns.");
                skippedRowCount++;
                continue;
            }

            try
            {
                TelemetryPoint point = new()
                {
                    Index = result.Count + 1,
                    Latitude = ParseDouble(parts[columnMap.Latitude]),
                    Longitude = ParseDouble(parts[columnMap.Longitude]),
                    SpeedKmh = ParseDouble(parts[columnMap.Speed]),
                    AccelZ = ParseDouble(parts[columnMap.AccelZ]),
                    AccelX = ParseDouble(parts[columnMap.AccelX]),
                    AccelY = ParseDouble(parts[columnMap.AccelY]),
                    LeanAngleDeg = ParseDouble(parts[columnMap.Lean])
                };

                result.Add(point);
            }
            catch (FormatException ex)
            {
                RegisterRowIssue(rowIssues, lineNumber, ex.Message);
                skippedRowCount++;
            }
        }

        if (!sawAnyDataRow)
            throw new InvalidOperationException("CSV file is empty or does not contain telemetry rows.");

        if (result.Count == 0 && skippedRowCount > 0)
            throw new InvalidOperationException(BuildNoValidRowsMessage(skippedRowCount, rowIssues));

        FillDistances(result);
        return new CsvTelemetryReadResult(result, skippedRowCount, rowIssues);
    }

    private static ColumnMap CreateColumnMap(string[] headers)
    {
        return new ColumnMap(
            FindColumn(headers, "широта", "РЁРёСЂРѕС‚Р°", "latitude", "lat"),
            FindColumn(headers, "долгота", "Р”РѕР»РіРѕС‚Р°", "longitude", "lon", "lng"),
            FindColumn(headers, "скорость", "РЎРєРѕСЂРѕСЃС‚СЊ", "speed", "speedkmh"),
            FindColumn(headers, "ускорение по Z", "РЈСЃРєРѕСЂРµРЅРёРµ РїРѕ Z", "accelZ", "az"),
            FindColumn(headers, "ускорение по X", "РЈСЃРєРѕСЂРµРЅРёРµ РїРѕ X", "accelX", "ax"),
            FindColumn(headers, "ускорение по Y", "РЈСЃРєРѕСЂРµРЅРёРµ РїРѕ Y", "accelY", "ay"),
            FindColumn(headers, "угол наклона", "РЈРіРѕР» РЅР°РєР»РѕРЅР°", "lean", "leanAngle", "roll"));
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
        HashSet<string> normalizedAliases = aliases
            .Select(Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headers.Length; i++)
        {
            if (normalizedAliases.Contains(headers[i]))
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

    private static void RegisterRowIssue(List<CsvTelemetryRowIssue> rowIssues, int lineNumber, string reason)
    {
        if (rowIssues.Count >= MaxReportedRowIssues)
            return;

        rowIssues.Add(new CsvTelemetryRowIssue(lineNumber, reason));
    }

    private static string BuildNoValidRowsMessage(int skippedRowCount, IReadOnlyList<CsvTelemetryRowIssue> rowIssues)
    {
        if (rowIssues.Count == 0)
            return $"CSV file does not contain valid telemetry rows. Skipped {skippedRowCount} malformed row(s).";

        CsvTelemetryRowIssue firstIssue = rowIssues[0];
        return $"CSV file does not contain valid telemetry rows. Skipped {skippedRowCount} malformed row(s). First issue: line {firstIssue.LineNumber}: {firstIssue.Reason}";
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

    private readonly record struct ColumnMap(
        int Latitude,
        int Longitude,
        int Speed,
        int AccelZ,
        int AccelX,
        int AccelY,
        int Lean);
}
