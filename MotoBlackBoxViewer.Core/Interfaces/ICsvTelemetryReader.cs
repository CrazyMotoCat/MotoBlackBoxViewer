using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.Core.Interfaces;

public interface ICsvTelemetryReader
{
    Task<IReadOnlyList<TelemetryPoint>> ReadAsync(string filePath, CancellationToken cancellationToken = default);
}
