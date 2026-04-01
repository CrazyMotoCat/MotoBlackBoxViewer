using System.IO;
using MotoBlackBoxViewer.App.ViewModels;

namespace MotoBlackBoxViewer.App.Services;

internal sealed class TelemetryWorkspaceLoadService
{
    private readonly TelemetryDataViewModel _data;
    private readonly TelemetryWorkspaceSynchronizationService _synchronization;

    public TelemetryWorkspaceLoadService(
        TelemetryDataViewModel data,
        TelemetryWorkspaceSynchronizationService synchronization)
    {
        _data = data;
        _synchronization = synchronization;
    }

    public async Task<string> LoadCsvAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await _data.LoadCsvAsync(filePath, cancellationToken);
        _synchronization.SynchronizeAfterLoad(updateStatus: false);

        string baseStatus = $"Файл {Path.GetFileName(filePath)} открыт: {_data.FilterMaximum} точек, в текущем диапазоне {_data.Points.Count}.";
        return string.IsNullOrWhiteSpace(_data.ImportDiagnosticsText)
            ? baseStatus
            : $"{baseStatus} {_data.ImportDiagnosticsText}";
    }
}
