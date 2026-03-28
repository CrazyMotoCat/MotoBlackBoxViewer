using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.App.ViewModels;
using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.App.Services;

internal sealed class TelemetryWorkspaceCoordinator
{
    private readonly TelemetryDataViewModel _data;
    private readonly TelemetrySelectionViewModel _selection;
    private readonly TelemetryPlaybackViewModel _playback;
    private readonly TelemetryMapViewModel _map;
    private readonly TelemetrySessionState _state;
    private readonly ISessionPersistenceCoordinator _sessionPersistenceCoordinator;
    private bool _suppressFilterHandling;

    public TelemetryWorkspaceCoordinator(
        TelemetryDataViewModel data,
        TelemetrySelectionViewModel selection,
        TelemetryPlaybackViewModel playback,
        TelemetryMapViewModel map,
        TelemetrySessionState state,
        ISessionPersistenceCoordinator sessionPersistenceCoordinator)
    {
        _data = data;
        _selection = selection;
        _playback = playback;
        _map = map;
        _state = state;
        _sessionPersistenceCoordinator = sessionPersistenceCoordinator;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        AppSessionSettings session = _sessionPersistenceCoordinator.Load();
        _playback.RestoreSpeed(session.SelectedPlaybackSpeedLabel);

        if (string.IsNullOrWhiteSpace(session.LastFilePath))
            return;

        if (!File.Exists(session.LastFilePath))
        {
            _data.StatusText = $"Последний файл не найден: {session.LastFilePath}";
            return;
        }

        try
        {
            _state.IsRestoringSession = true;
            _suppressFilterHandling = true;

            await _data.LoadCsvAsync(session.LastFilePath, cancellationToken);
            _data.RestoreFilterRange(session.FilterStartIndex, session.FilterEndIndex);

            TelemetryPoint? preferredPoint = _data.ApplyCurrentFilter(_state.SelectedPoint, updateStatus: false);
            _selection.SynchronizeWithVisiblePoints(preferredPoint);

            if (_data.HasPoints)
            {
                int restoredPosition = session.SelectedVisiblePosition <= 0
                    ? 1
                    : Math.Clamp(session.SelectedVisiblePosition, 1, _selection.PlaybackMaximum);

                _selection.PlaybackPosition = restoredPosition;
            }

            _map.RequestRefresh();
            _data.StatusText = $"Восстановлена последняя сессия: {Path.GetFileName(session.LastFilePath)}";
        }
        catch (Exception ex)
        {
            _data.StatusText = $"Не удалось восстановить последнюю сессию: {ex.Message}";
        }
        finally
        {
            _suppressFilterHandling = false;
            _state.IsRestoringSession = false;
        }
    }

    public void SaveSession() => FlushSession(includeSelectedPosition: true);

    public async Task LoadCsvAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _data.StatusText = "Читаю CSV...";
        _suppressFilterHandling = true;

        try
        {
            await _data.LoadCsvAsync(filePath, cancellationToken);

            TelemetryPoint? preferredPoint = _data.ApplyCurrentFilter(_state.SelectedPoint, updateStatus: false);
            _selection.SynchronizeWithVisiblePoints(preferredPoint);
            _map.RequestRefresh();

            _data.StatusText = $"Загружено точек: {_data.FilterMaximum}. Показано: {_data.Points.Count}. Файл: {Path.GetFileName(filePath)}";
            PersistSession(includeSelectedPosition: false);
        }
        finally
        {
            _suppressFilterHandling = false;
        }
    }

    public void Clear()
    {
        StopPlayback(updateStatus: false);
        _suppressFilterHandling = true;

        try
        {
            _data.Clear();
            _selection.Clear();
            _map.RequestRefresh();
            _data.StatusText = "Данные очищены.";
            PersistSession(includeSelectedPosition: false);
        }
        finally
        {
            _suppressFilterHandling = false;
        }
    }

    public void ResetFilter()
    {
        if (!_data.HasSourceData)
            return;

        StopPlayback(updateStatus: false);
        _suppressFilterHandling = true;

        try
        {
            _data.ResetFilterRange();
        }
        finally
        {
            _suppressFilterHandling = false;
        }

        ApplyFilterAndSynchronize(updateStatus: true);
    }

    public void OpenMapInBrowser()
    {
        string htmlPath = _map.ExportMapHtml();
        _map.OpenInBrowser(htmlPath);
        _data.StatusText = $"Карта подготовлена: {htmlPath}";
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
            _data.StatusText = $"Воспроизведение маршрута запущено ({_playback.SelectedPlaybackSpeed.Label}).";
    }

    public void StopPlayback(bool updateStatus = true)
    {
        bool stopped = _playback.Stop();
        if (updateStatus && stopped)
            _data.StatusText = "Воспроизведение остановлено.";
    }

    public void HandleDataPropertyChanged(string? propertyName)
    {
        if (_state.IsRestoringSession || _suppressFilterHandling)
            return;

        if (propertyName is nameof(TelemetryDataViewModel.FilterStartIndex) or nameof(TelemetryDataViewModel.FilterEndIndex))
        {
            StopPlayback(updateStatus: false);
            ApplyFilterAndSynchronize(updateStatus: true);
        }
    }

    public void HandleSelectionPropertyChanged(string? propertyName)
    {
        if (_state.IsRestoringSession)
            return;

        if (propertyName is nameof(TelemetrySelectionViewModel.SelectedPoint)
            or nameof(TelemetrySelectionViewModel.PlaybackPosition))
        {
            PersistSession(includeSelectedPosition: true);
        }

    }

    public void HandlePlaybackPropertyChanged(string? propertyName)
    {
        if (_state.IsRestoringSession)
            return;

        if (propertyName is not nameof(TelemetryPlaybackViewModel.SelectedPlaybackSpeed))
            return;

        PersistSession(includeSelectedPosition: false);

        if (_playback.IsPlaybackRunning)
            _data.StatusText = $"Скорость воспроизведения изменена: {_playback.SelectedPlaybackSpeed.Label}.";
    }

    private void ApplyFilterAndSynchronize(bool updateStatus)
    {
        TelemetryPoint? preferredPoint = _state.SelectedPoint;
        TelemetryPoint? nextPoint = _data.ApplyCurrentFilter(preferredPoint, updateStatus);
        _selection.SynchronizeWithVisiblePoints(nextPoint);
        _map.RequestRefresh();
        PersistSession(includeSelectedPosition: false);
    }

    private void PersistSession(bool includeSelectedPosition)
    {
        if (_state.IsRestoringSession)
            return;

        _sessionPersistenceCoordinator.Save(_state, _playback.SelectedPlaybackSpeed.Label, includeSelectedPosition);
    }

    private void FlushSession(bool includeSelectedPosition)
    {
        if (_state.IsRestoringSession)
            return;

        _sessionPersistenceCoordinator.Flush(_state, _playback.SelectedPlaybackSpeed.Label, includeSelectedPosition);
    }
}
