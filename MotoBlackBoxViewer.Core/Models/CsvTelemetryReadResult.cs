namespace MotoBlackBoxViewer.Core.Models;

public sealed class CsvTelemetryReadResult
{
    public CsvTelemetryReadResult(
        IReadOnlyList<TelemetryPoint> points,
        int skippedRowCount,
        IReadOnlyList<CsvTelemetryRowIssue> rowIssues,
        IReadOnlyList<string>? missingOptionalChannels = null,
        int? readRowCount = null)
    {
        Points = points;
        SkippedRowCount = skippedRowCount;
        RowIssues = rowIssues;
        MissingOptionalChannels = missingOptionalChannels ?? Array.Empty<string>();
        ReadRowCount = readRowCount ?? (points.Count + skippedRowCount);
    }

    public IReadOnlyList<TelemetryPoint> Points { get; }

    public int SkippedRowCount { get; }

    public IReadOnlyList<CsvTelemetryRowIssue> RowIssues { get; }

    public IReadOnlyList<string> MissingOptionalChannels { get; }

    public int ReadRowCount { get; }
}
