using System.IO;
using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.App.ViewModels;
using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.App.Services;

internal sealed class TelemetryWorkspaceCoordinator
{
    private readonly TelemetryDataViewModel _data;
    private readonly TelemetryPlaybackViewModel _playback;
    private readonly TelemetryMapViewModel _map;
    private readonly TelemetrySessionState _state;
    private readonly TelemetryWorkspaceSynchronizationService _synchronization;
    private readonly TelemetryWorkspacePersistenceService _persistence;
    private readonly TelemetryWorkspaceLoadService _load;
    private readonly TelemetryWorkspaceSessionRestoreService _sessionRestore;
    private int _suppressionDepth;

    public TelemetryWorkspaceCoordinator(
        TelemetryDataViewModel data,
        TelemetrySelectionViewModel selection,
        TelemetryPlaybackViewModel playback,
        TelemetryMapViewModel map,
        TelemetrySessionState state,
        ISessionPersistenceCoordinator sessionPersistenceCoordinator)
    {
        _data = data;
        _playback = playback;
        _map = map;
        _state = state;
        _synchronization = new TelemetryWorkspaceSynchronizationService(data, selection, map, state);
        _persistence = new TelemetryWorkspacePersistenceService(sessionPersistenceCoordinator);
        _load = new TelemetryWorkspaceLoadService(data, _synchronization);
        _sessionRestore = new TelemetryWorkspaceSessionRestoreService(data, playback, _synchronization);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        AppSessionSettings session = _persistence.Load();

        if (!File.Exists(session.LastFilePath))
        {
            _data.StatusText = $"Не удалось восстановить прошлую сессию: файл {Path.GetFileName(session.LastFilePath)} не найден.";
            return;
        }

        try
        {
            _state.IsRestoringSession = true;
            using IDisposable _ = EnterSuppression();

            string? status = await _sessionRestore.RestoreLastSessionAsync(session, cancellationToken);
            if (!string.IsNullOrWhiteSpace(status))
                _data.StatusText = status;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _data.StatusText = $"Не удалось восстановить прошлую сессию: {ex.Message}";
        }
        finally
        {
            _state.IsRestoringSession = false;
        }
    }

    public void SaveSession() => FlushSession(includeSelectedPosition: true);

    public async Task LoadCsvAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _data.StatusText = "Открываем CSV...";

        using IDisposable _ = EnterSuppression();

        try
        {
            _data.StatusText = await _load.LoadCsvAsync(filePath, cancellationToken);
            PersistSession(includeSelectedPosition: false);
        }
        catch (OperationCanceledException)
        {
            _data.StatusText = "Загрузка CSV отменена.";
            throw;
        }
        catch (Exception ex)
        {
            _data.StatusText = $"Не удалось открыть CSV: {ex.Message}";
            throw;
        }
    }

    public void Clear()
    {
        StopPlayback(updateStatus: false);

        using IDisposable _ = EnterSuppression();
        _synchronization.ClearWorkspace();
        _data.StatusText = "Сессия очищена.";
        PersistSession(includeSelectedPosition: false);
    }

    public void ResetFilter()
    {
        if (!_data.HasSourceData)
            return;

        StopPlayback(updateStatus: false);

        using IDisposable _ = EnterSuppression();
        _data.ResetFilterRange();

        ApplyFilterAndSynchronize(updateStatus: true);
    }

    public void OpenMapInBrowser()
    {
        string htmlPath = _map.ExportMapHtml();
        _map.OpenInBrowser(htmlPath);
        _data.StatusText = $"Карта открыта в браузере: {Path.GetFileName(htmlPath)}.";
    }

    public void TogglePlayback()
    {
        if (!_data.HasPoints)
            return;

        if (_playback.IsPlaybackRunning)
        {
            StopPlayback();
            return;
        }

        if (_playback.Start())
            _data.StatusText = $"Воспроизведение запущено · {_playback.SelectedPlaybackSpeed.Label}.";
    }

    public void StopPlayback(bool updateStatus = true)
    {
        bool stopped = _playback.Stop();
        if (updateStatus && stopped)
            _data.StatusText = "Воспроизведение остановлено.";
    }

    public void HandleDataPropertyChanged(string? propertyName)
    {
        if (_state.IsRestoringSession || IsReactiveHandlingSuppressed)
            return;

        if (propertyName is nameof(TelemetryDataViewModel.FilterStartIndex) or nameof(TelemetryDataViewModel.FilterEndIndex))
        {
            StopPlayback(updateStatus: false);
            ApplyFilterAndSynchronize(updateStatus: true);
        }
    }

    public void HandleSelectionPropertyChanged(string? propertyName)
    {
        if (_state.IsRestoringSession || IsReactiveHandlingSuppressed)
            return;

        if (propertyName is nameof(TelemetrySelectionViewModel.SelectedPoint)
            or nameof(TelemetrySelectionViewModel.PlaybackPosition))
        {
            PersistSession(includeSelectedPosition: true);
        }
    }

    public void HandlePlaybackPropertyChanged(string? propertyName)
    {
        if (_state.IsRestoringSession || IsReactiveHandlingSuppressed)
            return;

        if (propertyName is not nameof(TelemetryPlaybackViewModel.SelectedPlaybackSpeed))
            return;

        PersistSession(includeSelectedPosition: false);

        if (_playback.IsPlaybackRunning)
            _data.StatusText = $"Скорость воспроизведения: {_playback.SelectedPlaybackSpeed.Label}.";
    }

    private void ApplyFilterAndSynchronize(bool updateStatus)
    {
        using IDisposable _ = EnterSuppression();
        _synchronization.SynchronizeAfterLoad(updateStatus);
        PersistSession(includeSelectedPosition: false);
    }

    private void PersistSession(bool includeSelectedPosition)
        => _persistence.Save(_state, _playback.SelectedPlaybackSpeed.Label, includeSelectedPosition);

    private void FlushSession(bool includeSelectedPosition)
        => _persistence.Flush(_state, _playback.SelectedPlaybackSpeed.Label, includeSelectedPosition);

    private bool IsReactiveHandlingSuppressed => _suppressionDepth > 0;

    private IDisposable EnterSuppression()
    {
        _suppressionDepth++;
        return new SuppressionScope(this);
    }

    private sealed class SuppressionScope : IDisposable
    {
        private TelemetryWorkspaceCoordinator? _owner;

        public SuppressionScope(TelemetryWorkspaceCoordinator owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            if (_owner is null)
                return;

            _owner._suppressionDepth--;
            _owner = null;
        }
    }

}
