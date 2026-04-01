using System.Diagnostics;
using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Models;

namespace MotoBlackBoxViewer.App.Services;

public sealed class SessionPersistenceCoordinator : ISessionPersistenceCoordinator
{
    private static readonly TimeSpan DefaultSaveDelay = TimeSpan.FromMilliseconds(500);

    private readonly IAppSettingsService _settingsService;
    private readonly TimeSpan _saveDelay;
    private readonly Action<Exception>? _errorHandler;
    private readonly object _syncRoot = new();
    private AppSessionSettings? _pendingSnapshot;
    private CancellationTokenSource? _saveCts;

    public SessionPersistenceCoordinator(
        IAppSettingsService settingsService,
        TimeSpan? saveDelay = null,
        Action<Exception>? errorHandler = null)
    {
        _settingsService = settingsService;
        _saveDelay = saveDelay ?? DefaultSaveDelay;
        _errorHandler = errorHandler;
    }

    public event Action<Exception>? SaveFailed;

    public AppSessionSettings Load() => _settingsService.Load();

    public void Save(TelemetrySessionState state, string selectedPlaybackSpeedLabel, bool includeSelectedPosition)
    {
        AppSessionSettings snapshot = CreateSnapshot(state, selectedPlaybackSpeedLabel, includeSelectedPosition);
        ScheduleSave(snapshot);
    }

    public void Flush(TelemetrySessionState state, string selectedPlaybackSpeedLabel, bool includeSelectedPosition)
    {
        AppSessionSettings snapshot = CreateSnapshot(state, selectedPlaybackSpeedLabel, includeSelectedPosition);
        CancellationTokenSource? saveCts;

        lock (_syncRoot)
        {
            _pendingSnapshot = null;
            saveCts = _saveCts;
            _saveCts = null;
        }

        saveCts?.Cancel();
        saveCts?.Dispose();
        TrySaveSnapshot(snapshot);
    }

    private AppSessionSettings CreateSnapshot(TelemetrySessionState state, string selectedPlaybackSpeedLabel, bool includeSelectedPosition)
    {
        return new AppSessionSettings
        {
            LastFilePath = state.CurrentFilePath,
            FilterStartIndex = state.FilterStartIndex,
            FilterEndIndex = state.FilterEndIndex,
            SelectedChartWindowRadius = state.ChartWindowRadius,
            IsChartProfilingEnabled = state.IsChartProfilingEnabled,
            SelectedPlaybackSpeedLabel = selectedPlaybackSpeedLabel,
            SelectedVisiblePosition = includeSelectedPosition ? state.PlaybackPosition : 0
        };
    }

    private void ScheduleSave(AppSessionSettings snapshot)
    {
        CancellationTokenSource nextCts = new();
        CancellationTokenSource? previousCts;

        lock (_syncRoot)
        {
            _pendingSnapshot = snapshot;
            previousCts = _saveCts;
            _saveCts = nextCts;
        }

        previousCts?.Cancel();
        previousCts?.Dispose();
        _ = SaveLaterAsync(nextCts);
    }

    private async Task SaveLaterAsync(CancellationTokenSource saveCts)
    {
        try
        {
            await Task.Delay(_saveDelay, saveCts.Token);

            AppSessionSettings? snapshotToSave = null;
            bool shouldSave = false;

            lock (_syncRoot)
            {
                if (ReferenceEquals(_saveCts, saveCts))
                {
                    snapshotToSave = _pendingSnapshot;
                    _pendingSnapshot = null;
                    _saveCts = null;
                    shouldSave = snapshotToSave is not null;
                }
            }

            if (shouldSave)
                TrySaveSnapshot(snapshotToSave!);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ReportSaveError(ex);
        }
        finally
        {
            saveCts.Dispose();
        }
    }

    private void TrySaveSnapshot(AppSessionSettings snapshot)
    {
        try
        {
            _settingsService.Save(snapshot);
        }
        catch (Exception ex)
        {
            ReportSaveError(ex);
        }
    }

    private void ReportSaveError(Exception exception)
    {
        Trace.TraceError($"Failed to save session settings: {exception}");
        _errorHandler?.Invoke(exception);
        SaveFailed?.Invoke(exception);
    }
}
