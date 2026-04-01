using System.IO;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.App.ViewModels;

namespace MotoBlackBoxViewer.App.Services;

internal sealed class TelemetryWorkspaceSessionRestoreService
{
    private readonly TelemetryDataViewModel _data;
    private readonly TelemetryPlaybackViewModel _playback;
    private readonly TelemetryChartProfilingViewModel _chartProfiling;
    private readonly TelemetryWorkspaceSynchronizationService _synchronization;

    public TelemetryWorkspaceSessionRestoreService(
        TelemetryDataViewModel data,
        TelemetryPlaybackViewModel playback,
        TelemetryChartProfilingViewModel chartProfiling,
        TelemetryWorkspaceSynchronizationService synchronization)
    {
        _data = data;
        _playback = playback;
        _chartProfiling = chartProfiling;
        _synchronization = synchronization;
    }

    public async Task<string?> RestoreLastSessionAsync(AppSessionSettings session, CancellationToken cancellationToken = default)
    {
        _data.RestoreChartWindowRadius(session.SelectedChartWindowRadius);
        _playback.RestoreSpeed(session.SelectedPlaybackSpeedLabel);
        _chartProfiling.RestoreEnabled(session.IsChartProfilingEnabled);

        if (string.IsNullOrWhiteSpace(session.LastFilePath))
            return null;

        if (!File.Exists(session.LastFilePath))
            return $"Не удалось восстановить прошлую сессию: файл {Path.GetFileName(session.LastFilePath)} не найден.";

        await _data.LoadCsvAsync(session.LastFilePath, cancellationToken);
        _synchronization.RestoreSessionView(session);
        return $"Сессия восстановлена: {Path.GetFileName(session.LastFilePath)}.";
    }
}
