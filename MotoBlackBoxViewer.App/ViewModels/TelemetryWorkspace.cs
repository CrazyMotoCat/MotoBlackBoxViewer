using System.ComponentModel;
using MotoBlackBoxViewer.App.Helpers;
using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.App.Services;
using MotoBlackBoxViewer.Core.Interfaces;

namespace MotoBlackBoxViewer.App.ViewModels;

public sealed class TelemetryWorkspace : ObservableObject, IDisposable
{
    private readonly TelemetrySessionState _state = new();
    private readonly TelemetryWorkspaceCoordinator _coordinator;
    private bool _isDisposed;

    public TelemetryWorkspace(
        ICsvTelemetryReader reader,
        TelemetryDataProcessor dataProcessor,
        IMapExportService mapExportService,
        IPlaybackCoordinator playbackCoordinator,
        ISessionPersistenceCoordinator sessionPersistenceCoordinator)
    {
        Data = new TelemetryDataViewModel(reader, dataProcessor, _state);
        Selection = new TelemetrySelectionViewModel(Data, _state);
        Playback = new TelemetryPlaybackViewModel(Data, Selection, playbackCoordinator);
        Map = new TelemetryMapViewModel(Data, Selection, mapExportService, _state);
        _coordinator = new TelemetryWorkspaceCoordinator(
            Data,
            Selection,
            Playback,
            Map,
            _state,
            sessionPersistenceCoordinator);

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

    public Task InitializeAsync(CancellationToken cancellationToken = default)
        => _coordinator.InitializeAsync(cancellationToken);

    public void SaveSession() => _coordinator.SaveSession();

    public Task LoadCsvAsync(string filePath, CancellationToken cancellationToken = default)
        => _coordinator.LoadCsvAsync(filePath, cancellationToken);

    public void Clear() => _coordinator.Clear();

    public void ResetFilter() => _coordinator.ResetFilter();

    public void RequestMapRefresh() => Map.RequestRefresh();

    public void OpenMapInBrowser() => _coordinator.OpenMapInBrowser();

    public bool MoveSelection(int delta)
        => Selection.MoveSelection(delta);

    public void TogglePlayback() => _coordinator.TogglePlayback();

    public void StopPlayback(bool updateStatus = true) => _coordinator.StopPlayback(updateStatus);

    private void Data_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TelemetryDataViewModel.HasPoints) or nameof(TelemetryDataViewModel.HasSourceData))
        {
            RaisePropertyChanged(nameof(HasPoints));
            RaisePropertyChanged(nameof(HasSourceData));
        }

        _coordinator.HandleDataPropertyChanged(e.PropertyName);
    }

    private void Selection_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => _coordinator.HandleSelectionPropertyChanged(e.PropertyName);

    private void Playback_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => _coordinator.HandlePlaybackPropertyChanged(e.PropertyName);

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        Data.PropertyChanged -= Data_PropertyChanged;
        Selection.PropertyChanged -= Selection_PropertyChanged;
        Playback.PropertyChanged -= Playback_PropertyChanged;
        Map.Dispose();
        Selection.Dispose();
        Playback.Dispose();
    }
}
