using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.App.Services;
using MotoBlackBoxViewer.App.ViewModels;
using MotoBlackBoxViewer.Core.Interfaces;
using MotoBlackBoxViewer.Core.Models;
using MotoBlackBoxViewer.Core.Services;

namespace MotoBlackBoxViewer.Tests;

public sealed class TelemetryWorkspaceCoordinatorTests
{
    [Fact]
    public async Task LoadCsvAsync_LoadsVisibleDataRefreshesMapAndPersistsSession()
    {
        using TestContext context = CreateContext(new AppSessionSettings());

        await context.Coordinator.LoadCsvAsync("ride.csv");

        Assert.Equal(3, context.Data.Points.Count);
        Assert.Equal(3, context.Data.Statistics.PointCount);
        Assert.Equal(1, context.Map.RefreshVersion);
        Assert.Single(context.SessionPersistenceCoordinator.SaveCalls);
        Assert.False(context.SessionPersistenceCoordinator.SaveCalls[0].IncludeSelectedPosition);
        Assert.Contains("ride.csv", context.Data.StatusText);
    }

    [Fact]
    public async Task InitializeAsync_RestoresSessionFilterSelectionAndSpeed()
    {
        string filePath = CreateExistingTempFile();
        var session = new AppSessionSettings
        {
            LastFilePath = filePath,
            FilterStartIndex = 2,
            FilterEndIndex = 3,
            SelectedChartWindowRadius = 200,
            SelectedPlaybackSpeedLabel = "2x",
            SelectedVisiblePosition = 2
        };

        using TestContext context = CreateContext(session);

        await context.Coordinator.InitializeAsync();

        Assert.Equal("2x", context.Playback.SelectedPlaybackSpeed.Label);
        Assert.Equal(200, context.Data.ChartWindowRadius);
        Assert.Equal(2, context.Data.FilterStartIndex);
        Assert.Equal(3, context.Data.FilterEndIndex);
        Assert.Equal([2, 3], context.Data.Points.Select(p => p.Index));
        Assert.Equal(2, context.Selection.PlaybackPosition);
        Assert.Equal(3, context.Selection.SelectedPoint?.Index);
        Assert.Equal(1, context.Map.RefreshVersion);
        Assert.Contains(Path.GetFileName(filePath), context.Data.StatusText);

        File.Delete(filePath);
    }

    [Fact]
    public async Task InitializeAsync_WhenSessionFileIsMissing_SetsStatusAndDoesNotPersist()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), $"motobbv_missing_{Guid.NewGuid():N}.csv");
        var session = new AppSessionSettings
        {
            LastFilePath = missingPath
        };

        using TestContext context = CreateContext(session);

        await context.Coordinator.InitializeAsync();

        Assert.Contains("не найден", context.Data.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(context.SessionPersistenceCoordinator.SaveCalls);
        Assert.False(context.Data.HasSourceData);
    }

    [Fact]
    public async Task ResetFilter_RestoresFullRangeAndStopsPlayback()
    {
        using TestContext context = CreateContext(new AppSessionSettings());
        await context.Coordinator.LoadCsvAsync("ride.csv");
        context.Data.FilterStartIndex = 2;
        context.Data.FilterEndIndex = 2;
        context.Coordinator.HandleDataPropertyChanged(nameof(TelemetryDataViewModel.FilterStartIndex));
        context.PlaybackCoordinator.Start();

        context.Coordinator.ResetFilter();

        Assert.Equal(1, context.Data.FilterStartIndex);
        Assert.Equal(3, context.Data.FilterEndIndex);
        Assert.Equal([1, 2, 3], context.Data.Points.Select(p => p.Index));
        Assert.False(context.Playback.IsPlaybackRunning);
        Assert.Contains("Показан диапазон", context.Data.StatusText);
    }

    [Fact]
    public async Task ResetFilter_PersistsOnceWithoutSelectedPosition()
    {
        using TestContext context = CreateContext(new AppSessionSettings());
        await context.Coordinator.LoadCsvAsync("ride.csv");
        context.SessionPersistenceCoordinator.SaveCalls.Clear();
        context.Data.FilterStartIndex = 2;
        context.Coordinator.HandleDataPropertyChanged(nameof(TelemetryDataViewModel.FilterStartIndex));
        context.SessionPersistenceCoordinator.SaveCalls.Clear();

        context.Coordinator.ResetFilter();

        Assert.Single(context.SessionPersistenceCoordinator.SaveCalls);
        Assert.False(context.SessionPersistenceCoordinator.SaveCalls[0].IncludeSelectedPosition);
    }

    [Fact]
    public async Task Clear_ClearsAllDataRefreshesMapAndPersists()
    {
        using TestContext context = CreateContext(new AppSessionSettings());
        await context.Coordinator.LoadCsvAsync("ride.csv");

        context.Coordinator.Clear();

        Assert.Empty(context.Data.Points);
        Assert.False(context.Data.HasSourceData);
        Assert.Equal(2, context.Map.RefreshVersion);
        Assert.Equal(0, context.Selection.PlaybackPosition);
        Assert.Equal("Сессия очищена.", context.Data.StatusText);
        Assert.Equal(2, context.SessionPersistenceCoordinator.SaveCalls.Count);
    }

    [Fact]
    public async Task Clear_StopsPlaybackAndPersistsWithoutSelectedPosition()
    {
        using TestContext context = CreateContext(new AppSessionSettings());
        await context.Coordinator.LoadCsvAsync("ride.csv");
        context.Selection.PlaybackPosition = 2;
        context.Coordinator.HandleSelectionPropertyChanged(nameof(TelemetrySelectionViewModel.PlaybackPosition));
        context.Coordinator.TogglePlayback();
        context.SessionPersistenceCoordinator.SaveCalls.Clear();

        context.Coordinator.Clear();

        Assert.False(context.Playback.IsPlaybackRunning);
        Assert.Single(context.SessionPersistenceCoordinator.SaveCalls);
        Assert.False(context.SessionPersistenceCoordinator.SaveCalls[0].IncludeSelectedPosition);
        Assert.Equal(0, context.SessionPersistenceCoordinator.SaveCalls[0].PlaybackPosition);
    }

    [Fact]
    public async Task TogglePlayback_StartsAndStopsPlaybackWithStatus()
    {
        using TestContext context = CreateContext(new AppSessionSettings());
        await context.Coordinator.LoadCsvAsync("ride.csv");

        context.Coordinator.TogglePlayback();
        Assert.True(context.Playback.IsPlaybackRunning);
        Assert.Contains("Воспроизведение запущено", context.Data.StatusText);

        context.Coordinator.TogglePlayback();
        Assert.False(context.Playback.IsPlaybackRunning);
        Assert.Equal("Воспроизведение остановлено.", context.Data.StatusText);
    }

    [Fact]
    public async Task TogglePlayback_WhenSelectionIsAtEnd_RestartsFromFirstPoint()
    {
        using TestContext context = CreateContext(new AppSessionSettings());
        await context.Coordinator.LoadCsvAsync("ride.csv");
        context.Selection.PlaybackPosition = context.Selection.PlaybackMaximum;

        context.Coordinator.TogglePlayback();

        Assert.True(context.Playback.IsPlaybackRunning);
        Assert.Equal(1, context.Selection.PlaybackPosition);
        Assert.Equal(1, context.Selection.SelectedPoint?.Index);
    }

    [Fact]
    public async Task HandlePlaybackPropertyChanged_PersistsSpeedAndUpdatesStatusWhileRunning()
    {
        using TestContext context = CreateContext(new AppSessionSettings());
        await context.Coordinator.LoadCsvAsync("ride.csv");
        context.Coordinator.TogglePlayback();

        PlaybackSpeedOption fasterSpeed = context.Playback.PlaybackSpeedOptions.First(option => option.Label == "2x");
        context.Playback.SelectedPlaybackSpeed = fasterSpeed;
        context.Coordinator.HandlePlaybackPropertyChanged(nameof(TelemetryPlaybackViewModel.SelectedPlaybackSpeed));

        Assert.Equal("2x", context.Playback.SelectedPlaybackSpeed.Label);
        Assert.Equal(2, context.SessionPersistenceCoordinator.SaveCalls.Count);
        Assert.Contains("Скорость воспроизведения", context.Data.StatusText);
    }

    [Fact]
    public async Task HandleSelectionPropertyChanged_PersistsSelectedPlaybackPosition()
    {
        using TestContext context = CreateContext(new AppSessionSettings());
        await context.Coordinator.LoadCsvAsync("ride.csv");
        context.SessionPersistenceCoordinator.SaveCalls.Clear();
        context.Selection.PlaybackPosition = 2;

        context.Coordinator.HandleSelectionPropertyChanged(nameof(TelemetrySelectionViewModel.PlaybackPosition));

        Assert.Single(context.SessionPersistenceCoordinator.SaveCalls);
        Assert.True(context.SessionPersistenceCoordinator.SaveCalls[0].IncludeSelectedPosition);
        Assert.Equal(2, context.SessionPersistenceCoordinator.SaveCalls[0].PlaybackPosition);
    }

    [Fact]
    public async Task HandleDataPropertyChanged_WhenChartWindowChanges_PersistsWithoutSelectedPosition()
    {
        using TestContext context = CreateContext(new AppSessionSettings());
        await context.Coordinator.LoadCsvAsync("ride.csv");
        context.SessionPersistenceCoordinator.SaveCalls.Clear();
        context.Data.SelectedChartWindow = context.Data.ChartWindowOptions.First(option => option.Radius == 50);

        context.Coordinator.HandleDataPropertyChanged(nameof(TelemetryDataViewModel.SelectedChartWindow));

        Assert.Single(context.SessionPersistenceCoordinator.SaveCalls);
        Assert.False(context.SessionPersistenceCoordinator.SaveCalls[0].IncludeSelectedPosition);
        Assert.Equal(50, context.State.ChartWindowRadius);
    }

    [Fact]
    public async Task LoadCsvAsync_WhenReaderFails_SetsErrorStatusAndDoesNotPersist()
    {
        using TestContext context = CreateContext(
            new AppSessionSettings(),
            reader: new ThrowingCsvTelemetryReader(new InvalidOperationException("broken csv")));

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Coordinator.LoadCsvAsync("broken.csv"));

        Assert.Equal("broken csv", ex.Message);
        Assert.Equal("Не удалось открыть CSV: broken csv", context.Data.StatusText);
        Assert.Empty(context.SessionPersistenceCoordinator.SaveCalls);
    }

    [Fact]
    public async Task InitializeAsync_WhenCanceled_PropagatesCancellationAndResetsRestoreFlag()
    {
        string filePath = CreateExistingTempFile();
        var session = new AppSessionSettings
        {
            LastFilePath = filePath
        };

        using TestContext context = CreateContext(
            session,
            reader: new ThrowingCsvTelemetryReader(new OperationCanceledException("restore canceled")));

        await Assert.ThrowsAsync<OperationCanceledException>(() => context.Coordinator.InitializeAsync());

        Assert.False(context.State.IsRestoringSession);
        Assert.Empty(context.SessionPersistenceCoordinator.SaveCalls);

        File.Delete(filePath);
    }

    private static TestContext CreateContext(AppSessionSettings session, ICsvTelemetryReader? reader = null)
    {
        TelemetrySessionState state = new();
        ICsvTelemetryReader resolvedReader = reader ?? new StubCsvTelemetryReader(CreatePoints());
        TelemetryDataProcessor dataProcessor = new(new TelemetryAnalyzer());
        TelemetryDataViewModel data = new(resolvedReader, dataProcessor, state);
        TelemetrySelectionViewModel selection = new(data, state);
        StubPlaybackCoordinator playbackCoordinator = new();
        TelemetryPlaybackViewModel playback = new(data, selection, playbackCoordinator);
        StubMapExportService mapExportService = new();
        TelemetryMapViewModel map = new(data, selection, mapExportService, state);
        RecordingSessionPersistenceCoordinator sessionPersistenceCoordinator = new(session);
        TelemetryWorkspaceCoordinator coordinator = new(
            data,
            selection,
            playback,
            map,
            state,
            sessionPersistenceCoordinator);

        return new TestContext(
            coordinator,
            data,
            selection,
            playback,
            map,
            playbackCoordinator,
            sessionPersistenceCoordinator,
            state);
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

    private sealed class TestContext : IDisposable
    {
        public TestContext(
            TelemetryWorkspaceCoordinator coordinator,
            TelemetryDataViewModel data,
            TelemetrySelectionViewModel selection,
            TelemetryPlaybackViewModel playback,
            TelemetryMapViewModel map,
            StubPlaybackCoordinator playbackCoordinator,
            RecordingSessionPersistenceCoordinator sessionPersistenceCoordinator,
            TelemetrySessionState state)
        {
            Coordinator = coordinator;
            Data = data;
            Selection = selection;
            Playback = playback;
            Map = map;
            PlaybackCoordinator = playbackCoordinator;
            SessionPersistenceCoordinator = sessionPersistenceCoordinator;
            State = state;
        }

        public TelemetryWorkspaceCoordinator Coordinator { get; }

        public TelemetryDataViewModel Data { get; }

        public TelemetrySelectionViewModel Selection { get; }

        public TelemetryPlaybackViewModel Playback { get; }

        public TelemetryMapViewModel Map { get; }

        public StubPlaybackCoordinator PlaybackCoordinator { get; }

        public RecordingSessionPersistenceCoordinator SessionPersistenceCoordinator { get; }

        public TelemetrySessionState State { get; }

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

    private sealed class ThrowingCsvTelemetryReader : ICsvTelemetryReader
    {
        private readonly Exception _exception;

        public ThrowingCsvTelemetryReader(Exception exception)
        {
            _exception = exception;
        }

        public Task<IReadOnlyList<TelemetryPoint>> ReadAsync(string filePath, CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<TelemetryPoint>>(_exception);
    }

    private sealed class StubMapExportService : IMapExportService
    {
        public string GetTemplatePath() => "template.html";

        public string BuildRouteJson(IReadOnlyList<TelemetryPoint> points)
            => $"[{string.Join(",", points.Select(p => p.Index))}]";

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

        public event EventHandler? Tick;

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

        public void RaiseTick() => Tick?.Invoke(this, EventArgs.Empty);
    }

    private sealed class RecordingSessionPersistenceCoordinator : ISessionPersistenceCoordinator
    {
        private readonly AppSessionSettings _loadSettings;

        public RecordingSessionPersistenceCoordinator(AppSessionSettings loadSettings)
        {
            _loadSettings = loadSettings;
        }

        public List<SaveCall> SaveCalls { get; } = [];

        public AppSessionSettings Load() => new()
        {
            LastFilePath = _loadSettings.LastFilePath,
            FilterStartIndex = _loadSettings.FilterStartIndex,
            FilterEndIndex = _loadSettings.FilterEndIndex,
            SelectedChartWindowRadius = _loadSettings.SelectedChartWindowRadius,
            SelectedPlaybackSpeedLabel = _loadSettings.SelectedPlaybackSpeedLabel,
            SelectedVisiblePosition = _loadSettings.SelectedVisiblePosition
        };

        public void Save(TelemetrySessionState state, string selectedPlaybackSpeedLabel, bool includeSelectedPosition)
        {
            SaveCalls.Add(new SaveCall(state.CurrentFilePath, selectedPlaybackSpeedLabel, includeSelectedPosition, state.PlaybackPosition));
        }

        public void Flush(TelemetrySessionState state, string selectedPlaybackSpeedLabel, bool includeSelectedPosition)
        {
            Save(state, selectedPlaybackSpeedLabel, includeSelectedPosition);
        }
    }

    private sealed record SaveCall(
        string? CurrentFilePath,
        string SelectedPlaybackSpeedLabel,
        bool IncludeSelectedPosition,
        int PlaybackPosition);
}
