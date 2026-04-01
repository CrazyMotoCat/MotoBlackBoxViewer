namespace MotoBlackBoxViewer.Core.Models;

public sealed class CsvTelemetryReadResult
{
    public CsvTelemetryReadResult(
        IReadOnlyList<TelemetryPoint> points,
        int skippedRowCount,
        IReadOnlyList<CsvTelemetryRowIssue> rowIssues)
    {
        Points = points;
        SkippedRowCount = skippedRowCount;
        RowIssues = rowIssues;
    }

    public IReadOnlyList<TelemetryPoint> Points { get; }

    public int SkippedRowCount { get; }

    public IReadOnlyList<CsvTelemetryRowIssue> RowIssues { get; }
}
