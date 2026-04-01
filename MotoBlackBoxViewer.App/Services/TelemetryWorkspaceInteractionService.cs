using System.IO;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.App.ViewModels;

namespace MotoBlackBoxViewer.App.Services;

internal sealed class TelemetryWorkspaceInteractionService
{
    private readonly TelemetryDataViewModel _data;
    private readonly TelemetryPlaybackViewModel _playback;
    private readonly TelemetryMapViewModel _map;
    private readonly TelemetryChartProfilingViewModel _chartProfiling;
    private readonly TelemetrySessionState _state;
    private readonly TelemetryWorkspaceSynchronizationService _synchronization;
    private readonly TelemetryWorkspacePersistenceService _persistence;
    private readonly Func<IDisposable> _enterSuppression;
    private readonly Func<bool> _isReactiveHandlingSuppressed;

    public TelemetryWorkspaceInteractionService(
        TelemetryDataViewModel data,
        TelemetryPlaybackViewModel playback,
        TelemetryMapViewModel map,
        TelemetryChartProfilingViewModel chartProfiling,
        TelemetrySessionState state,
        TelemetryWorkspaceSynchronizationService synchronization,
        TelemetryWorkspacePersistenceService persistence,
        Func<IDisposable> enterSuppression,
        Func<bool> isReactiveHandlingSuppressed)
    {
        _data = data;
        _playback = playback;
        _map = map;
        _chartProfiling = chartProfiling;
        _state = state;
        _synchronization = synchronization;
        _persistence = persistence;
        _enterSuppression = enterSuppression;
        _isReactiveHandlingSuppressed = isReactiveHandlingSuppressed;
    }

    public void Clear()
    {
        StopPlayback(updateStatus: false);

        using IDisposable _ = _enterSuppression();
        _synchronization.ClearWorkspace();
        _data.StatusText = "\u0421\u0435\u0441\u0441\u0438\u044f \u043e\u0447\u0438\u0449\u0435\u043d\u0430.";
        PersistSession(includeSelectedPosition: false);
    }

    public void ResetFilter()
    {
        if (!_data.HasSourceData)
            return;

        StopPlayback(updateStatus: false);

        using IDisposable _ = _enterSuppression();
        _data.ResetFilterRange();
        ApplyFilterAndSynchronize(updateStatus: true);
    }

    public void OpenMapInBrowser()
    {
        try
        {
            string htmlPath = _map.ExportMapHtml();
            _map.OpenInBrowser(htmlPath);
            _data.StatusText = $"\u041a\u0430\u0440\u0442\u0430 \u043e\u0442\u043a\u0440\u044b\u0442\u0430 \u0432 \u0431\u0440\u0430\u0443\u0437\u0435\u0440\u0435: {Path.GetFileName(htmlPath)}.";
        }
        catch (Exception ex)
        {
            _data.StatusText = $"\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u044d\u043a\u0441\u043f\u043e\u0440\u0442\u0438\u0440\u043e\u0432\u0430\u0442\u044c \u0438\u043b\u0438 \u043e\u0442\u043a\u0440\u044b\u0442\u044c \u043a\u0430\u0440\u0442\u0443: {ex.Message}";
        }
    }

    public void ToggleChartProfiling()
    {
        _chartProfiling.IsEnabled = !_chartProfiling.IsEnabled;
        _data.StatusText = _chartProfiling.IsEnabled
            ? "\u0420\u0435\u0436\u0438\u043c \u043f\u0440\u043e\u0444\u0438\u043b\u0438\u0440\u043e\u0432\u0430\u043d\u0438\u044f \u0433\u0440\u0430\u0444\u0438\u043a\u043e\u0432 \u0432\u043a\u043b\u044e\u0447\u0435\u043d. \u0422\u0430\u0439\u043c\u0438\u043d\u0433\u0438 \u043d\u0430\u043a\u0430\u043f\u043b\u0438\u0432\u0430\u044e\u0442\u0441\u044f \u0434\u043b\u044f \u0442\u0435\u043a\u0443\u0449\u0435\u0439 \u0441\u0435\u0441\u0441\u0438\u0438."
            : "\u0420\u0435\u0436\u0438\u043c \u043f\u0440\u043e\u0444\u0438\u043b\u0438\u0440\u043e\u0432\u0430\u043d\u0438\u044f \u0433\u0440\u0430\u0444\u0438\u043a\u043e\u0432 \u0432\u044b\u043a\u043b\u044e\u0447\u0435\u043d.";
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
            _data.StatusText = $"\u0412\u043e\u0441\u043f\u0440\u043e\u0438\u0437\u0432\u0435\u0434\u0435\u043d\u0438\u0435 \u0437\u0430\u043f\u0443\u0449\u0435\u043d\u043e \u00b7 {_playback.SelectedPlaybackSpeed.Label}.";
    }

    public void StopPlayback(bool updateStatus = true)
    {
        bool stopped = _playback.Stop();
        if (updateStatus && stopped)
            _data.StatusText = "\u0412\u043e\u0441\u043f\u0440\u043e\u0438\u0437\u0432\u0435\u0434\u0435\u043d\u0438\u0435 \u043e\u0441\u0442\u0430\u043d\u043e\u0432\u043b\u0435\u043d\u043e.";
    }

    public void HandleDataPropertyChanged(string? propertyName)
    {
        if (ShouldIgnoreReactiveChange())
            return;

        if (propertyName is nameof(TelemetryDataViewModel.FilterStartIndex) or nameof(TelemetryDataViewModel.FilterEndIndex))
        {
            StopPlayback(updateStatus: false);
            ApplyFilterAndSynchronize(updateStatus: true);
            return;
        }

        if (propertyName is nameof(TelemetryDataViewModel.SelectedChartWindow))
            PersistSession(includeSelectedPosition: false);
    }

    public void HandleSelectionPropertyChanged(string? propertyName)
    {
        if (ShouldIgnoreReactiveChange())
            return;

        if (propertyName is nameof(TelemetrySelectionViewModel.SelectedPoint)
            or nameof(TelemetrySelectionViewModel.PlaybackPosition))
        {
            PersistSession(includeSelectedPosition: true);
        }
    }

    public void HandlePlaybackPropertyChanged(string? propertyName)
    {
        if (ShouldIgnoreReactiveChange())
            return;

        if (propertyName is not nameof(TelemetryPlaybackViewModel.SelectedPlaybackSpeed))
            return;

        PersistSession(includeSelectedPosition: false);

        if (_playback.IsPlaybackRunning)
            _data.StatusText = $"\u0421\u043a\u043e\u0440\u043e\u0441\u0442\u044c \u0432\u043e\u0441\u043f\u0440\u043e\u0438\u0437\u0432\u0435\u0434\u0435\u043d\u0438\u044f: {_playback.SelectedPlaybackSpeed.Label}.";
    }

    public void HandleChartProfilingPropertyChanged(string? propertyName)
    {
        if (ShouldIgnoreReactiveChange())
            return;

        if (propertyName is nameof(TelemetryChartProfilingViewModel.IsEnabled))
            PersistSession(includeSelectedPosition: false);
    }

    private void ApplyFilterAndSynchronize(bool updateStatus)
    {
        using IDisposable _ = _enterSuppression();
        _synchronization.SynchronizeAfterLoad(updateStatus);
        PersistSession(includeSelectedPosition: false);
    }

    private void PersistSession(bool includeSelectedPosition)
        => _persistence.Save(_state, _playback.SelectedPlaybackSpeed.Label, includeSelectedPosition);

    private bool ShouldIgnoreReactiveChange()
        => _state.IsRestoringSession || _isReactiveHandlingSuppressed();
}
