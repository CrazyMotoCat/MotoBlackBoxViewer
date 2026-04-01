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
            ChartWindowRadius = 200,
            IsChartProfilingEnabled = true,
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
        Assert.True(settingsService.SavedSnapshots[0].IsChartProfilingEnabled);
    }

    [Fact]
    public void Flush_CancelsPendingSaveAndPersistsLatestSnapshotImmediately()
    {
        var settingsService = new RecordingAppSettingsService();
        var coordinator = new SessionPersistenceCoordinator(settingsService, TimeSpan.FromSeconds(5));
        var state = new TelemetrySessionState
        {
            CurrentFilePath = "ride.csv",
            ChartWindowRadius = 50,
            PlaybackPosition = 7
        };

        coordinator.Save(state, "2x", includeSelectedPosition: true);
        state.PlaybackPosition = 9;

        coordinator.Flush(state, "4x", includeSelectedPosition: true);

        Assert.Single(settingsService.SavedSnapshots);
        AppSessionSettings snapshot = settingsService.SavedSnapshots[0];
        Assert.Equal("4x", snapshot.SelectedPlaybackSpeedLabel);
        Assert.Equal(50, snapshot.SelectedChartWindowRadius);
        Assert.Equal(9, snapshot.SelectedVisiblePosition);
    }

    [Fact]
    public async Task Save_WhenBackgroundWriteFails_ReportsErrorWithoutThrowing()
    {
        var settingsService = new ThrowingAppSettingsService();
        Exception? reportedError = null;
        Exception? raisedEventError = null;
        var coordinator = new SessionPersistenceCoordinator(
            settingsService,
            TimeSpan.FromMilliseconds(20),
            errorHandler: ex => reportedError = ex);
        coordinator.SaveFailed += ex => raisedEventError = ex;
        var state = new TelemetrySessionState
        {
            CurrentFilePath = "ride.csv",
            PlaybackPosition = 2
        };

        coordinator.Save(state, "1x", includeSelectedPosition: true);

        await Task.Delay(100);

        Assert.NotNull(reportedError);
        Assert.IsType<IOException>(reportedError);
        Assert.NotNull(raisedEventError);
        Assert.IsType<IOException>(raisedEventError);
    }

    [Fact]
    public void Flush_WhenWriteFails_ReportsErrorWithoutThrowing()
    {
        var settingsService = new ThrowingAppSettingsService();
        Exception? reportedError = null;
        Exception? raisedEventError = null;
        var coordinator = new SessionPersistenceCoordinator(
            settingsService,
            errorHandler: ex => reportedError = ex);
        coordinator.SaveFailed += ex => raisedEventError = ex;
        var state = new TelemetrySessionState
        {
            CurrentFilePath = "ride.csv",
            PlaybackPosition = 9
        };

        var exception = Record.Exception(() => coordinator.Flush(state, "1x", includeSelectedPosition: true));

        Assert.Null(exception);
        Assert.NotNull(reportedError);
        Assert.IsType<IOException>(reportedError);
        Assert.NotNull(raisedEventError);
        Assert.IsType<IOException>(raisedEventError);
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
                SelectedChartWindowRadius = settings.SelectedChartWindowRadius,
                IsChartProfilingEnabled = settings.IsChartProfilingEnabled,
                SelectedPlaybackSpeedLabel = settings.SelectedPlaybackSpeedLabel,
                SelectedVisiblePosition = settings.SelectedVisiblePosition
            });
        }
    }

    private sealed class ThrowingAppSettingsService : IAppSettingsService
    {
        public AppSessionSettings Load() => new();

        public void Save(AppSessionSettings settings)
            => throw new IOException("Disk full.");
    }
}
