using System.ComponentModel;
using MotoBlackBoxViewer.App.Helpers;
using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.Core.Interfaces;
using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.App.ViewModels;

public sealed class TelemetryWorkspace : ObservableObject, IDisposable
{
    private readonly TelemetrySessionState _state = new();
    private readonly ISessionPersistenceCoordinator _sessionPersistenceCoordinator;
    private bool _isDisposed;
    private bool _suppressFilterHandling;

    public TelemetryWorkspace(
        ICsvTelemetryReader reader,
        ITelemetryAnalyzer analyzer,
        IMapExportService mapExportService,
        IPlaybackCoordinator playbackCoordinator,
        ISessionPersistenceCoordinator sessionPersistenceCoordinator)
    {
        _sessionPersistenceCoordinator = sessionPersistenceCoordinator;

        Data = new TelemetryDataViewModel(reader, analyzer, _state);
        Selection = new TelemetrySelectionViewModel(Data, _state);
        Playback = new TelemetryPlaybackViewModel(Data, Selection, playbackCoordinator);
        Map = new TelemetryMapViewModel(Data, Selection, mapExportService, _state);

        Data.PropertyChanged += Data_PropertyChanged;
        Selection.PropertyChanged += Selection_PropertyChanged;
        Playback.PropertyChanged += Playback_PropertyChanged;
    }

    public TelemetryDataViewModel Data { get; }

    public TelemetrySelectionViewModel Selection { get; }

    public TelemetryPlaybackViewModel Playback { get; }

    public TelemetryMapViewModel Map { get; }

    public bool HasPoints => Data.HasPoints;

    public bool HasSourceData => Data.HasSourceData;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        AppSessionSettings session = _sessionPersistenceCoordinator.Load();
        Playback.RestoreSpeed(session.SelectedPlaybackSpeedLabel);

        if (string.IsNullOrWhiteSpace(session.LastFilePath))
            return;

        if (!File.Exists(session.LastFilePath))
        {
            Data.StatusText = $"Последний файл не найден: {session.LastFilePath}";
            return;
        }

        try
        {
            _state.IsRestoringSession = true;
            _suppressFilterHandling = true;

            await Data.LoadCsvAsync(session.LastFilePath, cancellationToken);
            Data.RestoreFilterRange(session.FilterStartIndex, session.FilterEndIndex);

            TelemetryPoint? preferredPoint = Data.ApplyCurrentFilter(_state.SelectedPoint, updateStatus: false);
            Selection.SynchronizeWithVisiblePoints(preferredPoint);

            if (Data.HasPoints)
            {
                int restoredPosition = session.SelectedVisiblePosition <= 0
                    ? 1
                    : Math.Clamp(session.SelectedVisiblePosition, 1, Selection.PlaybackMaximum);

                Selection.PlaybackPosition = restoredPosition;
            }

            Map.RequestRefresh();
            Data.StatusText = $"Восстановлена последняя сессия: {Path.GetFileName(session.LastFilePath)}";
        }
        catch (Exception ex)
        {
            Data.StatusText = $"Не удалось восстановить последнюю сессию: {ex.Message}";
        }
        finally
        {
            _suppressFilterHandling = false;
            _state.IsRestoringSession = false;
        }
    }

    public void SaveSession() => PersistSession(includeSelectedPosition: true);

    public async Task LoadCsvAsync(string filePath, CancellationToken cancellationToken = default)
    {
        Data.StatusText = "Читаю CSV...";
        _suppressFilterHandling = true;

        try
        {
            await Data.LoadCsvAsync(filePath, cancellationToken);

            TelemetryPoint? preferredPoint = Data.ApplyCurrentFilter(_state.SelectedPoint, updateStatus: false);
            Selection.SynchronizeWithVisiblePoints(preferredPoint);
            Map.RequestRefresh();

            Data.StatusText = $"Загружено точек: {Data.FilterMaximum}. Показано: {Data.Points.Count}. Файл: {Path.GetFileName(filePath)}";
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
            Data.Clear();
            Selection.Clear();
            Map.RequestRefresh();
            Data.StatusText = "Данные очищены.";
            PersistSession(includeSelectedPosition: false);
        }
        finally
        {
            _suppressFilterHandling = false;
        }
    }

    public void ResetFilter()
    {
        if (!Data.HasSourceData)
            return;

        StopPlayback(updateStatus: false);
        _suppressFilterHandling = true;

        try
        {
            Data.ResetFilterRange();
        }
        finally
        {
            _suppressFilterHandling = false;
        }

        ApplyFilterAndSynchronize(updateStatus: true);
    }

    public void RequestMapRefresh() => Map.RequestRefresh();

    public void OpenMapInBrowser()
    {
        string htmlPath = Map.ExportMapHtml();
        Map.OpenInBrowser(htmlPath);
        Data.StatusText = $"Карта подготовлена: {htmlPath}";
    }

    public bool MoveSelection(int delta)
        => Selection.MoveSelection(delta);

    public void TogglePlayback()
    {
        if (!Data.HasPoints)
            return;

        if (Playback.IsPlaybackRunning)
        {
            StopPlayback();
            return;
        }

        if (Playback.Start())
            Data.StatusText = $"Воспроизведение маршрута запущено ({Playback.SelectedPlaybackSpeed.Label}).";
    }

    public void StopPlayback(bool updateStatus = true)
    {
        bool stopped = Playback.Stop();
        if (updateStatus && stopped)
            Data.StatusText = "Воспроизведение остановлено.";
    }

    private void ApplyFilterAndSynchronize(bool updateStatus)
    {
        TelemetryPoint? preferredPoint = _state.SelectedPoint;
        TelemetryPoint? nextPoint = Data.ApplyCurrentFilter(preferredPoint, updateStatus);
        Selection.SynchronizeWithVisiblePoints(nextPoint);
        Map.RequestRefresh();
        PersistSession(includeSelectedPosition: false);
    }

    private void Data_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TelemetryDataViewModel.HasPoints) or nameof(TelemetryDataViewModel.HasSourceData))
        {
            RaisePropertyChanged(nameof(HasPoints));
            RaisePropertyChanged(nameof(HasSourceData));
        }

        if (_state.IsRestoringSession || _suppressFilterHandling)
            return;

        if (e.PropertyName is nameof(TelemetryDataViewModel.FilterStartIndex) or nameof(TelemetryDataViewModel.FilterEndIndex))
        {
            StopPlayback(updateStatus: false);
            ApplyFilterAndSynchronize(updateStatus: true);
        }
    }

    private void Selection_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_state.IsRestoringSession)
            return;

        if (e.PropertyName is nameof(TelemetrySelectionViewModel.SelectedPoint)
            or nameof(TelemetrySelectionViewModel.PlaybackPosition))
        {
            PersistSession(includeSelectedPosition: true);
        }

        if (e.PropertyName is nameof(TelemetrySelectionViewModel.SelectedPointIndex))
            Map.RequestRefresh();
    }

    private void Playback_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_state.IsRestoringSession)
            return;

        if (e.PropertyName is nameof(TelemetryPlaybackViewModel.SelectedPlaybackSpeed))
        {
            PersistSession(includeSelectedPosition: false);

            if (Playback.IsPlaybackRunning)
                Data.StatusText = $"Скорость воспроизведения изменена: {Playback.SelectedPlaybackSpeed.Label}.";
        }
    }

    private void PersistSession(bool includeSelectedPosition)
    {
        if (_state.IsRestoringSession)
            return;

        _sessionPersistenceCoordinator.Save(_state, Playback.SelectedPlaybackSpeed.Label, includeSelectedPosition);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        Data.PropertyChanged -= Data_PropertyChanged;
        Selection.PropertyChanged -= Selection_PropertyChanged;
        Playback.PropertyChanged -= Playback_PropertyChanged;
        Playback.Dispose();
    }
}
