using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Models;

namespace MotoBlackBoxViewer.App.Services;

internal sealed class TelemetryWorkspacePersistenceService
{
    private readonly ISessionPersistenceCoordinator _sessionPersistenceCoordinator;
    private SaveRequest? _lastSaveRequest;

    public TelemetryWorkspacePersistenceService(ISessionPersistenceCoordinator sessionPersistenceCoordinator)
    {
        _sessionPersistenceCoordinator = sessionPersistenceCoordinator;
    }

    public AppSessionSettings Load()
        => _sessionPersistenceCoordinator.Load();

    public void Save(TelemetrySessionState state, string selectedPlaybackSpeedLabel, bool includeSelectedPosition)
    {
        if (state.IsRestoringSession)
            return;

        SaveRequest request = CreateSaveRequest(state, selectedPlaybackSpeedLabel, includeSelectedPosition);
        if (Equals(_lastSaveRequest, request))
            return;

        _lastSaveRequest = request;
        _sessionPersistenceCoordinator.Save(state, selectedPlaybackSpeedLabel, includeSelectedPosition);
    }

    public void Flush(TelemetrySessionState state, string selectedPlaybackSpeedLabel, bool includeSelectedPosition)
    {
        if (state.IsRestoringSession)
            return;

        _lastSaveRequest = CreateSaveRequest(state, selectedPlaybackSpeedLabel, includeSelectedPosition);
        _sessionPersistenceCoordinator.Flush(state, selectedPlaybackSpeedLabel, includeSelectedPosition);
    }

    private static SaveRequest CreateSaveRequest(TelemetrySessionState state, string selectedPlaybackSpeedLabel, bool includeSelectedPosition)
    {
        return new SaveRequest(
            state.CurrentFilePath,
            state.FilterStartIndex,
            state.FilterEndIndex,
            state.ChartWindowRadius,
            selectedPlaybackSpeedLabel,
            includeSelectedPosition,
            includeSelectedPosition ? state.PlaybackPosition : 0);
    }

    private sealed record SaveRequest(
        string? CurrentFilePath,
        int FilterStartIndex,
        int FilterEndIndex,
        int ChartWindowRadius,
        string SelectedPlaybackSpeedLabel,
        bool IncludeSelectedPosition,
        int PlaybackPosition);
}
