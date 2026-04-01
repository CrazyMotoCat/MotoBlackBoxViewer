using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.Core.Interfaces;

public interface ICsvTelemetryReader
{
    Task<CsvTelemetryReadResult> ReadAsync(string filePath, CancellationToken cancellationToken = default);
}
