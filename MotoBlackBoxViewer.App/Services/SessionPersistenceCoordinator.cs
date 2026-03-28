using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Models;

namespace MotoBlackBoxViewer.App.Services;

public sealed class SessionPersistenceCoordinator : ISessionPersistenceCoordinator
{
    private readonly IAppSettingsService _settingsService;

    public SessionPersistenceCoordinator(IAppSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public AppSessionSettings Load() => _settingsService.Load();

    public void Save(TelemetrySessionState state, string selectedPlaybackSpeedLabel, bool includeSelectedPosition)
    {
        AppSessionSettings snapshot = new()
        {
            LastFilePath = state.CurrentFilePath,
            FilterStartIndex = state.FilterStartIndex,
            FilterEndIndex = state.FilterEndIndex,
            SelectedPlaybackSpeedLabel = selectedPlaybackSpeedLabel,
            SelectedVisiblePosition = includeSelectedPosition ? state.PlaybackPosition : 0
        };

        _settingsService.Save(snapshot);
    }
}
