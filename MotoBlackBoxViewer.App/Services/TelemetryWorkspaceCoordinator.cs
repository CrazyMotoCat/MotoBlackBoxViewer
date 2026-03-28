using System.IO;
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
    private int _suppressionDepth;
    private SaveRequest? _lastSaveRequest;

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
            _data.StatusText = $"Не удалось восстановить прошлую сессию: файл {Path.GetFileName(session.LastFilePath)} не найден.";
            return;
        }

        try
        {
            _state.IsRestoringSession = true;
            using IDisposable _ = EnterSuppression();

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
            _data.StatusText = $"Сессия восстановлена: {Path.GetFileName(session.LastFilePath)}.";
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
            await _data.LoadCsvAsync(filePath, cancellationToken);

            TelemetryPoint? preferredPoint = _data.ApplyCurrentFilter(_state.SelectedPoint, updateStatus: false);
            _selection.SynchronizeWithVisiblePoints(preferredPoint);
            _map.RequestRefresh();

            _data.StatusText = $"Файл {Path.GetFileName(filePath)} открыт: {_data.FilterMaximum} точек, в текущем диапазоне {_data.Points.Count}.";
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
        _data.Clear();
        _selection.Clear();
        _map.RequestRefresh();
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

        SaveRequest request = CreateSaveRequest(includeSelectedPosition);
        if (Equals(_lastSaveRequest, request))
            return;

        _lastSaveRequest = request;
        _sessionPersistenceCoordinator.Save(_state, _playback.SelectedPlaybackSpeed.Label, includeSelectedPosition);
    }

    private void FlushSession(bool includeSelectedPosition)
    {
        if (_state.IsRestoringSession)
            return;

        _lastSaveRequest = CreateSaveRequest(includeSelectedPosition);
        _sessionPersistenceCoordinator.Flush(_state, _playback.SelectedPlaybackSpeed.Label, includeSelectedPosition);
    }

    private SaveRequest CreateSaveRequest(bool includeSelectedPosition)
    {
        return new SaveRequest(
            _state.CurrentFilePath,
            _state.FilterStartIndex,
            _state.FilterEndIndex,
            _playback.SelectedPlaybackSpeed.Label,
            includeSelectedPosition,
            includeSelectedPosition ? _state.PlaybackPosition : 0);
    }

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

    private sealed record SaveRequest(
        string? CurrentFilePath,
        int FilterStartIndex,
        int FilterEndIndex,
        string SelectedPlaybackSpeedLabel,
        bool IncludeSelectedPosition,
        int PlaybackPosition);
}
