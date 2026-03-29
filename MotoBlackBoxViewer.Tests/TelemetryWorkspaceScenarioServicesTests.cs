using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.App.Services;
using MotoBlackBoxViewer.App.ViewModels;
using MotoBlackBoxViewer.Core.Interfaces;
using MotoBlackBoxViewer.Core.Models;
using MotoBlackBoxViewer.Core.Services;

namespace MotoBlackBoxViewer.Tests;

public sealed class TelemetryWorkspaceScenarioServicesTests
{
    [Fact]
    public void PersistenceService_Save_DeduplicatesIdenticalRequests()
    {
        TelemetrySessionState state = CreateState();
        RecordingSessionPersistenceCoordinator coordinator = new();
        TelemetryWorkspacePersistenceService service = new(coordinator);

        service.Save(state, "1x", includeSelectedPosition: false);
        service.Save(state, "1x", includeSelectedPosition: false);

        Assert.Single(coordinator.SaveCalls);
    }

    [Fact]
    public void PersistenceService_Save_TreatsSelectedPositionPersistenceAsDistinctRequest()
    {
        TelemetrySessionState state = CreateState();
        RecordingSessionPersistenceCoordinator coordinator = new();
        TelemetryWorkspacePersistenceService service = new(coordinator);

        service.Save(state, "1x", includeSelectedPosition: false);
        service.Save(state, "1x", includeSelectedPosition: true);

        Assert.Equal(2, coordinator.SaveCalls.Count);
        Assert.False(coordinator.SaveCalls[0].IncludeSelectedPosition);
        Assert.True(coordinator.SaveCalls[1].IncludeSelectedPosition);
        Assert.Equal(state.PlaybackPosition, coordinator.SaveCalls[1].PlaybackPosition);
    }

    [Fact]
    public void PersistenceService_Flush_UpdatesLastRequestSnapshot()
    {
        TelemetrySessionState state = CreateState();
        RecordingSessionPersistenceCoordinator coordinator = new();
        TelemetryWorkspacePersistenceService service = new(coordinator);

        service.Save(state, "2x", includeSelectedPosition: true);
        service.Flush(state, "2x", includeSelectedPosition: true);
        service.Save(state, "2x", includeSelectedPosition: true);

        Assert.Equal(2, coordinator.SaveCalls.Count);
    }

    [Fact]
    public async Task LoadService_LoadCsvAsync_LoadsVisibleDataAndRefreshesMap()
    {
        using ScenarioContext context = CreateScenarioContext();
        TelemetryWorkspaceLoadService service = new(context.Data, context.Synchronization);

        string status = await service.LoadCsvAsync("ride.csv");

        Assert.Equal([1, 2, 3], context.Data.Points.Select(point => point.Index));
        Assert.Equal(1, context.Selection.PlaybackPosition);
        Assert.Equal(1, context.Selection.SelectedPoint?.Index);
        Assert.Equal(1, context.Map.RefreshVersion);
        Assert.Contains("ride.csv", status);
        Assert.Contains("3", status);
    }

    [Fact]
    public async Task SessionRestoreService_RestoreLastSessionAsync_RestoresFilterSelectionSpeedAndMap()
    {
        using ScenarioContext context = CreateScenarioContext();
        TelemetryWorkspaceSessionRestoreService service = new(
            context.Data,
            context.Playback,
            context.Synchronization);
        string filePath = CreateExistingTempFile();
        AppSessionSettings session = new()
        {
            LastFilePath = filePath,
            FilterStartIndex = 2,
            FilterEndIndex = 3,
            SelectedChartWindowRadius = 200,
            SelectedPlaybackSpeedLabel = "2x",
            SelectedVisiblePosition = 99
        };

        string? status = await service.RestoreLastSessionAsync(session);

        Assert.Equal("2x", context.Playback.SelectedPlaybackSpeed.Label);
        Assert.Equal(200, context.Data.ChartWindowRadius);
        Assert.Equal([2, 3], context.Data.Points.Select(point => point.Index));
        Assert.Equal(2, context.Selection.PlaybackPosition);
        Assert.Equal(3, context.Selection.SelectedPoint?.Index);
        Assert.Equal(1, context.Map.RefreshVersion);
        Assert.Contains(Path.GetFileName(filePath), status);

        File.Delete(filePath);
    }

    [Fact]
    public async Task SessionRestoreService_RestoreLastSessionAsync_WithoutFilePathOnlyRestoresSpeed()
    {
        using ScenarioContext context = CreateScenarioContext();
        TelemetryWorkspaceSessionRestoreService service = new(
            context.Data,
            context.Playback,
            context.Synchronization);
        AppSessionSettings session = new()
        {
            SelectedChartWindowRadius = 50,
            SelectedPlaybackSpeedLabel = "0.5x"
        };

        string? status = await service.RestoreLastSessionAsync(session);

        Assert.Null(status);
        Assert.Equal(50, context.Data.ChartWindowRadius);
        Assert.Equal("0.5x", context.Playback.SelectedPlaybackSpeed.Label);
        Assert.False(context.Data.HasSourceData);
        Assert.Equal(0, context.Map.RefreshVersion);
    }

    private static TelemetrySessionState CreateState()
    {
        return new TelemetrySessionState
        {
            CurrentFilePath = "ride.csv",
            FilterStartIndex = 2,
            FilterEndIndex = 4,
            ChartWindowRadius = 200,
            PlaybackPosition = 3
        };
    }

    private static ScenarioContext CreateScenarioContext()
    {
        TelemetrySessionState state = new();
        ICsvTelemetryReader reader = new StubCsvTelemetryReader(CreatePoints());
        TelemetryDataProcessor dataProcessor = new(new TelemetryAnalyzer());
        TelemetryDataViewModel data = new(reader, dataProcessor, state);
        TelemetrySelectionViewModel selection = new(data, state);
        TelemetryPlaybackViewModel playback = new(data, selection, new StubPlaybackCoordinator());
        TelemetryMapViewModel map = new(data, selection, new StubMapExportService(), state);
        TelemetryWorkspaceSynchronizationService synchronization = new(data, selection, map, state);

        return new ScenarioContext(data, selection, playback, map, synchronization);
    }

    private static IReadOnlyList<TelemetryPoint> CreatePoints()
    {
        return
        [
            new TelemetryPoint { Index = 1, Latitude = 43.1, Longitude = 131.1, SpeedKmh = 10, AccelX = 0.1, AccelY = 0.2, AccelZ = 0.3, LeanAngleDeg = 1, DistanceFromStartMeters = 0 },
            new TelemetryPoint { Index = 2, Latitude = 43.2, Longitude = 131.2, SpeedKmh = 20, AccelX = 0.4, AccelY = 0.5, AccelZ = 0.6, LeanAngleDeg = 2, DistanceFromStartMeters = 100 },
            new TelemetryPoint { Index = 3, Latitude = 43.3, Longitude = 131.3, SpeedKmh = 30, AccelX = 0.7, AccelY = 0.8, AccelZ = 0.9, LeanAngleDeg = 3, DistanceFromStartMeters = 250 }
        ];
    }

    private static string CreateExistingTempFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"motobbv_session_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, "stub");
        return path;
    }

    private sealed class ScenarioContext : IDisposable
    {
        public ScenarioContext(
            TelemetryDataViewModel data,
            TelemetrySelectionViewModel selection,
            TelemetryPlaybackViewModel playback,
            TelemetryMapViewModel map,
            TelemetryWorkspaceSynchronizationService synchronization)
        {
            Data = data;
            Selection = selection;
            Playback = playback;
            Map = map;
            Synchronization = synchronization;
        }

        public TelemetryDataViewModel Data { get; }

        public TelemetrySelectionViewModel Selection { get; }

        public TelemetryPlaybackViewModel Playback { get; }

        public TelemetryMapViewModel Map { get; }

        public TelemetryWorkspaceSynchronizationService Synchronization { get; }

        public void Dispose()
        {
            Map.Dispose();
            Selection.Dispose();
            Playback.Dispose();
        }
    }

    private sealed class StubCsvTelemetryReader : ICsvTelemetryReader
    {
        private readonly IReadOnlyList<TelemetryPoint> _points;

        public StubCsvTelemetryReader(IReadOnlyList<TelemetryPoint> points)
        {
            _points = points;
        }

        public Task<IReadOnlyList<TelemetryPoint>> ReadAsync(string filePath, CancellationToken cancellationToken = default)
            => Task.FromResult(_points);
    }

    private sealed class StubMapExportService : IMapExportService
    {
        public string GetTemplatePath() => "template.html";

        public string BuildRouteJson(IReadOnlyList<TelemetryPoint> points)
            => $"[{string.Join(",", points.Select(point => point.Index))}]";

        public string ExportHtml(IReadOnlyList<TelemetryPoint> points, string outputDirectory)
            => Path.Combine(outputDirectory, "map.html");

        public void OpenInBrowser(string htmlPath)
        {
        }
    }

    private sealed class StubPlaybackCoordinator : IPlaybackCoordinator
    {
        public StubPlaybackCoordinator()
        {
            SpeedOptions =
            [
                new PlaybackSpeedOption("0.5x", 0.5),
                new PlaybackSpeedOption("1x", 1.0),
                new PlaybackSpeedOption("2x", 2.0)
            ];
            SelectedSpeed = SpeedOptions[1];
        }

        public event EventHandler? Tick
        {
            add { }
            remove { }
        }

        public IReadOnlyList<PlaybackSpeedOption> SpeedOptions { get; }

        public PlaybackSpeedOption SelectedSpeed { get; private set; }

        public int IntervalMilliseconds => (int)Math.Round(350d / SelectedSpeed.Multiplier);

        public bool IsRunning { get; private set; }

        public bool SetSelectedSpeed(PlaybackSpeedOption option)
        {
            if (Equals(option, SelectedSpeed))
                return false;

            SelectedSpeed = option;
            return true;
        }

        public void RestoreSpeed(string? label)
        {
            PlaybackSpeedOption? match = SpeedOptions.FirstOrDefault(option => option.Label == label);
            if (match is not null)
                SelectedSpeed = match;
        }

        public void Start() => IsRunning = true;

        public void Stop() => IsRunning = false;

        public void Dispose()
        {
        }
    }

    private sealed class RecordingSessionPersistenceCoordinator : ISessionPersistenceCoordinator
    {
        public List<SaveCall> SaveCalls { get; } = [];

        public AppSessionSettings Load() => new();

        public void Save(TelemetrySessionState state, string selectedPlaybackSpeedLabel, bool includeSelectedPosition)
        {
            SaveCalls.Add(new SaveCall(state.CurrentFilePath, selectedPlaybackSpeedLabel, includeSelectedPosition, state.PlaybackPosition));
        }

        public void Flush(TelemetrySessionState state, string selectedPlaybackSpeedLabel, bool includeSelectedPosition)
            => Save(state, selectedPlaybackSpeedLabel, includeSelectedPosition);
    }

    private sealed record SaveCall(
        string? CurrentFilePath,
        string SelectedPlaybackSpeedLabel,
        bool IncludeSelectedPosition,
        int PlaybackPosition);
}
