namespace MotoBlackBoxViewer.App.Models;

public sealed record ChartSeriesDefinition(string Label, IReadOnlyList<double> Values, string ColorHex);
