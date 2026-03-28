using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.App.Services;

namespace MotoBlackBoxViewer.Tests;

public sealed class SessionPersistenceCoordinatorTests
{
    [Fact]
    public async Task Save_DebouncesRapidUpdates()
    {
        var settingsService = new RecordingAppSettingsService();
        var coordinator = new SessionPersistenceCoordinator(settingsService, TimeSpan.FromMilliseconds(40));
        var state = new TelemetrySessionState
        {
            CurrentFilePath = "ride.csv",
            FilterStartIndex = 1,
            FilterEndIndex = 42,
            PlaybackPosition = 3
        };

        coordinator.Save(state, "1x", includeSelectedPosition: true);
        state.PlaybackPosition = 4;
        coordinator.Save(state, "1x", includeSelectedPosition: true);
        state.PlaybackPosition = 5;
        coordinator.Save(state, "1x", includeSelectedPosition: true);

        await Task.Delay(120);

        Assert.Single(settingsService.SavedSnapshots);
        Assert.Equal(5, settingsService.SavedSnapshots[0].SelectedVisiblePosition);
    }

    [Fact]
    public void Flush_CancelsPendingSaveAndPersistsLatestSnapshotImmediately()
    {
        var settingsService = new RecordingAppSettingsService();
        var coordinator = new SessionPersistenceCoordinator(settingsService, TimeSpan.FromSeconds(5));
        var state = new TelemetrySessionState
        {
            CurrentFilePath = "ride.csv",
            PlaybackPosition = 7
        };

        coordinator.Save(state, "2x", includeSelectedPosition: true);
        state.PlaybackPosition = 9;

        coordinator.Flush(state, "4x", includeSelectedPosition: true);

        Assert.Single(settingsService.SavedSnapshots);
        AppSessionSettings snapshot = settingsService.SavedSnapshots[0];
        Assert.Equal("4x", snapshot.SelectedPlaybackSpeedLabel);
        Assert.Equal(9, snapshot.SelectedVisiblePosition);
    }

    private sealed class RecordingAppSettingsService : IAppSettingsService
    {
        public List<AppSessionSettings> SavedSnapshots { get; } = [];

        public AppSessionSettings Load() => new();

        public void Save(AppSessionSettings settings)
        {
            SavedSnapshots.Add(new AppSessionSettings
            {
                LastFilePath = settings.LastFilePath,
                FilterStartIndex = settings.FilterStartIndex,
                FilterEndIndex = settings.FilterEndIndex,
                SelectedPlaybackSpeedLabel = settings.SelectedPlaybackSpeedLabel,
                SelectedVisiblePosition = settings.SelectedVisiblePosition
            });
        }
    }
}
