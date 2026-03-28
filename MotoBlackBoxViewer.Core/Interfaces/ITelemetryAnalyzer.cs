using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.Core.Interfaces;

public interface ITelemetryAnalyzer
{
    TelemetryStatistics Analyze(IReadOnlyList<TelemetryPoint> points);
}
