namespace MotoBlackBoxViewer.App.Models;

public sealed record ChartProfilingOperationSummary(
    string Stage,
    int Samples,
    double TotalMilliseconds,
    double AverageMilliseconds,
    double MaxMilliseconds,
    int LastInputPointCount,
    int LastOutputPointCount,
    string LastOperation,
    string LastDetail)
{
    public string LastIoSummary => $"in {LastInputPointCount} / out {LastOutputPointCount}";

    public string LastEventSummary => string.IsNullOrWhiteSpace(LastDetail)
        ? LastOperation
        : $"{LastOperation} · {LastDetail}";
}
