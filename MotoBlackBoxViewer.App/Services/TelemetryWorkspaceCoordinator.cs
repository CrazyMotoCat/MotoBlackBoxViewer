using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.App.ViewModels;

namespace MotoBlackBoxViewer.App.Services;

internal sealed class TelemetryWorkspaceCoordinator
{
    private readonly TelemetryDataViewModel _data;
    private readonly TelemetryPlaybackViewModel _playback;
    private readonly TelemetrySessionState _state;
    private readonly TelemetryWorkspacePersistenceService _persistence;
    private readonly TelemetryWorkspaceLoadService _load;
    private readonly TelemetryWorkspaceSessionRestoreService _sessionRestore;
    private readonly TelemetryWorkspaceInteractionService _interaction;
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
        _state = state;

        TelemetryWorkspaceSynchronizationService synchronization = new(data, selection, map, state);
        _persistence = new TelemetryWorkspacePersistenceService(sessionPersistenceCoordinator);
        _load = new TelemetryWorkspaceLoadService(data, synchronization);
        _sessionRestore = new TelemetryWorkspaceSessionRestoreService(data, playback, synchronization);
        _interaction = new TelemetryWorkspaceInteractionService(
            data,
            playback,
            map,
            state,
            synchronization,
            _persistence,
            EnterSuppression,
            () => IsReactiveHandlingSuppressed);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        AppSessionSettings session = _persistence.Load();

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
            _data.StatusText = $"\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u0432\u043e\u0441\u0441\u0442\u0430\u043d\u043e\u0432\u0438\u0442\u044c \u043f\u0440\u043e\u0448\u043b\u0443\u044e \u0441\u0435\u0441\u0441\u0438\u044e: {ex.Message}";
        }
        finally
        {
            _state.IsRestoringSession = false;
        }
    }

    public void SaveSession() => FlushSession(includeSelectedPosition: true);

    public async Task LoadCsvAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _data.StatusText = "\u041e\u0442\u043a\u0440\u044b\u0432\u0430\u0435\u043c CSV...";

        using IDisposable _ = EnterSuppression();

        try
        {
            _data.StatusText = await _load.LoadCsvAsync(filePath, cancellationToken);
            PersistSession(includeSelectedPosition: false);
        }
        catch (OperationCanceledException)
        {
            _data.StatusText = "\u0417\u0430\u0433\u0440\u0443\u0437\u043a\u0430 CSV \u043e\u0442\u043c\u0435\u043d\u0435\u043d\u0430.";
            throw;
        }
        catch (Exception ex)
        {
            _data.StatusText = $"\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u043e\u0442\u043a\u0440\u044b\u0442\u044c CSV: {ex.Message}";
            throw;
        }
    }

    public void Clear() => _interaction.Clear();

    public void ResetFilter() => _interaction.ResetFilter();

    public void OpenMapInBrowser() => _interaction.OpenMapInBrowser();

    public void TogglePlayback() => _interaction.TogglePlayback();

    public void StopPlayback(bool updateStatus = true) => _interaction.StopPlayback(updateStatus);

    public void HandleDataPropertyChanged(string? propertyName) => _interaction.HandleDataPropertyChanged(propertyName);

    public void HandleSelectionPropertyChanged(string? propertyName) => _interaction.HandleSelectionPropertyChanged(propertyName);

    public void HandlePlaybackPropertyChanged(string? propertyName) => _interaction.HandlePlaybackPropertyChanged(propertyName);

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
