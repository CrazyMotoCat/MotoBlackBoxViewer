using System.ComponentModel;
using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.App.Services;
using MotoBlackBoxViewer.App.ViewModels;
using MotoBlackBoxViewer.Core.Interfaces;
using MotoBlackBoxViewer.Core.Models;
using MotoBlackBoxViewer.Core.Services;

namespace MotoBlackBoxViewer.Tests;

public sealed class TelemetryAppViewModelTests
{
    [Fact]
    public async Task LoadCsvAsync_PopulatesVisibleDataImmediately()
    {
        var state = new TelemetrySessionState();
        var reader = new StubCsvTelemetryReader(
        [
            CreatePoint(1, 10),
            CreatePoint(2, 20),
            CreatePoint(3, 30)
        ]);
        var dataProcessor = new TelemetryDataProcessor(new TelemetryAnalyzer());
        var viewModel = new TelemetryDataViewModel(reader, dataProcessor, state);

        await viewModel.LoadCsvAsync("ride.csv");

        Assert.True(viewModel.HasSourceData);
        Assert.True(viewModel.HasPoints);
        Assert.Equal(3, viewModel.Points.Count);
        Assert.Equal(3, viewModel.Statistics.PointCount);
        Assert.Equal([10d, 20d, 30d], viewModel.SpeedSeries);
    }

    [Fact]
    public void CreateVisibleData_ReturnsContiguousSliceForRange()
    {
        var processor = new TelemetryDataProcessor(new TelemetryAnalyzer());
        List<TelemetryPoint> allPoints =
        [
            CreatePoint(1, 10),
            CreatePoint(2, 20),
            CreatePoint(3, 30),
            CreatePoint(4, 40),
            CreatePoint(5, 50)
        ];

        TelemetryVisibleData visibleData = processor.CreateVisibleData(allPoints, startIndex: 2, endIndex: 4);

        Assert.Equal([2, 3, 4], visibleData.Points.Select(p => p.Index));
        Assert.Equal(1, visibleData.VisiblePositionsByPointIndex[2]);
        Assert.Equal(2, visibleData.VisiblePositionsByPointIndex[3]);
        Assert.Equal(3, visibleData.VisiblePositionsByPointIndex[4]);
        Assert.Equal(3, visibleData.Statistics.PointCount);
    }

    [Fact]
    public void CreateVisibleData_UsesSeriesSliceViewsInsteadOfRebuildingChartArrays()
    {
        var processor = new TelemetryDataProcessor(new TelemetryAnalyzer());
        List<TelemetryPoint> allPoints =
        [
            CreatePoint(1, 10),
            CreatePoint(2, 20),
            CreatePoint(3, 30),
            CreatePoint(4, 40),
            CreatePoint(5, 50)
        ];

        TelemetryVisibleData visibleData = processor.CreateVisibleData(allPoints, startIndex: 2, endIndex: 4);

        Assert.IsType<ArraySegment<double>>(visibleData.SeriesSnapshot.SpeedSeries);
        Assert.IsType<ArraySegment<double>>(visibleData.SeriesSnapshot.LeanSeries);
        Assert.IsType<ArraySegment<double>>(visibleData.SeriesSnapshot.AccelXSeries);
        Assert.Equal([20d, 30d, 40d], visibleData.SeriesSnapshot.SpeedSeries);
    }

    [Fact]
    public void CreateVisibleData_UsesTimeWeightedStatisticsForFilteredRange()
    {
        var processor = new TelemetryDataProcessor(new TelemetryAnalyzer());
        List<TelemetryPoint> allPoints =
        [
            CreatePoint(1, 5, 0),
            CreatePoint(2, 10, 100),
            CreatePoint(3, 100, 110),
            CreatePoint(4, 100, 1110)
        ];

        TelemetryVisibleData visibleData = processor.CreateVisibleData(allPoints, startIndex: 2, endIndex: 4);
        double expectedAverageSpeed =
            1.01d /
            ((0.01d / 55d) + (1d / 100d));

        Assert.Equal([2, 3, 4], visibleData.Points.Select(p => p.Index));
        Assert.NotEqual((10d + 100d + 100d) / 3d, visibleData.Statistics.AverageSpeedKmh, 3);
        Assert.Equal(expectedAverageSpeed, visibleData.Statistics.AverageSpeedKmh, 3);
    }

    [Fact]
    public void Selection_Dispose_UnsubscribesFromDataEvents()
    {
        var data = CreateLoadedDataViewModel();
        var selection = new TelemetrySelectionViewModel(data, new TelemetrySessionState());
        int propertyChangedCount = 0;
        selection.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TelemetrySelectionViewModel.PlaybackMaximum))
                propertyChangedCount++;
        };

        data.FilterStartIndex = 2;
        data.ApplyCurrentFilter(selection.SelectedPoint, updateStatus: false);
        Assert.True(propertyChangedCount > 0);

        selection.Dispose();
        propertyChangedCount = 0;

        data.FilterStartIndex = 1;
        data.ApplyCurrentFilter(selection.SelectedPoint, updateStatus: false);

        Assert.Equal(0, propertyChangedCount);
    }

    [Fact]
    public void Map_Dispose_UnsubscribesFromSelectionAndDataEvents()
    {
        var data = CreateLoadedDataViewModel();
        var selection = new TelemetrySelectionViewModel(data, new TelemetrySessionState());
        var map = new TelemetryMapViewModel(data, selection, new StubMapExportService(), new TelemetrySessionState());
        int propertyChangedCount = 0;
        map.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(TelemetryMapViewModel.RouteJson) or nameof(TelemetryMapViewModel.SelectedPointIndex))
                propertyChangedCount++;
        };

        selection.SelectedPoint = data.Points[0];
        data.ApplyCurrentFilter(selection.SelectedPoint, updateStatus: false);
        Assert.True(propertyChangedCount > 0);

        map.Dispose();
        propertyChangedCount = 0;

        selection.SelectedPoint = data.Points[^1];
        data.FilterStartIndex = 2;
        data.ApplyCurrentFilter(selection.SelectedPoint, updateStatus: false);

        Assert.Equal(0, propertyChangedCount);
        selection.Dispose();
    }

    [Fact]
    public async Task Workspace_LoadCsvAsync_PersistsSessionOnceDespiteInternalSelectionSync()
    {
        using WorkspaceTestContext context = CreateWorkspaceContext();

        await context.Workspace.LoadCsvAsync("ride.csv");

        Assert.Single(context.SessionPersistenceCoordinator.SaveCalls);
        Assert.False(context.SessionPersistenceCoordinator.SaveCalls[0].IncludeSelectedPosition);
        Assert.Equal(1, context.Workspace.Selection.PlaybackPosition);
    }

    [Fact]
    public async Task Workspace_FilterChange_PersistsSessionOnceWithoutSelectionEchoSave()
    {
        using WorkspaceTestContext context = CreateWorkspaceContext();
        await context.Workspace.LoadCsvAsync("ride.csv");
        context.SessionPersistenceCoordinator.SaveCalls.Clear();

        context.Workspace.Data.FilterStartIndex = 2;

        Assert.Single(context.SessionPersistenceCoordinator.SaveCalls);
        Assert.False(context.SessionPersistenceCoordinator.SaveCalls[0].IncludeSelectedPosition);
        Assert.Equal(2, context.Workspace.Selection.SelectedPoint?.Index);
        Assert.Equal(1, context.Workspace.Selection.PlaybackPosition);
    }

    private static TelemetryDataViewModel CreateLoadedDataViewModel()
    {
        var state = new TelemetrySessionState();
        var reader = new StubCsvTelemetryReader(
        [
            CreatePoint(1, 10),
            CreatePoint(2, 20),
            CreatePoint(3, 30)
        ]);
        var data = new TelemetryDataViewModel(reader, new TelemetryDataProcessor(new TelemetryAnalyzer()), state);
        data.LoadCsvAsync("ride.csv").GetAwaiter().GetResult();
        return data;
    }

    private static WorkspaceTestContext CreateWorkspaceContext()
    {
        StubCsvTelemetryReader reader = new(
        [
            CreatePoint(1, 10),
            CreatePoint(2, 20),
            CreatePoint(3, 30)
        ]);
        TelemetryDataProcessor dataProcessor = new(new TelemetryAnalyzer());
        RecordingSessionPersistenceCoordinator sessionPersistenceCoordinator = new();
        TelemetryWorkspace workspace = new(
            reader,
            dataProcessor,
            new StubMapExportService(),
            new StubPlaybackCoordinator(),
            sessionPersistenceCoordinator);

        return new WorkspaceTestContext(workspace, sessionPersistenceCoordinator);
    }

    private static TelemetryPoint CreatePoint(int index, double speedKmh, double? distanceFromStartMeters = null)
    {
        return new TelemetryPoint
        {
            Index = index,
            Latitude = 43 + index,
            Longitude = 131 + index,
            SpeedKmh = speedKmh,
            AccelX = index * 0.1,
            AccelY = index * 0.2,
            AccelZ = index * 0.3,
            LeanAngleDeg = index * 1.5,
            DistanceFromStartMeters = distanceFromStartMeters ?? index * 100
        };
    }

    private sealed class StubCsvTelemetryReader : ICsvTelemetryReader
    {
        private readonly IReadOnlyList<TelemetryPoint> _points;

        public StubCsvTelemetryReader(IReadOnlyList<TelemetryPoint> points)
        {
            _points = points;
        }

        public Task<CsvTelemetryReadResult> ReadAsync(string filePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new CsvTelemetryReadResult(_points, 0, Array.Empty<CsvTelemetryRowIssue>()));
    }

    private sealed class StubMapExportService : IMapExportService
    {
        public string GetTemplatePath() => "template.html";

        public string BuildRouteJson(IReadOnlyList<TelemetryPoint> points)
            => $"[{string.Join(",", points.Select(p => p.Index))}]";

        public string ExportHtml(IReadOnlyList<TelemetryPoint> points, string baseDirectory)
            => Path.Combine(baseDirectory, "map.html");

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
        public event Action<Exception>? SaveFailed
        {
            add { }
            remove { }
        }

        public List<SaveCall> SaveCalls { get; } = [];

        public AppSessionSettings Load() => new();

        public void Save(TelemetrySessionState state, string selectedPlaybackSpeedLabel, bool includeSelectedPosition)
        {
            SaveCalls.Add(new SaveCall(state.CurrentFilePath, selectedPlaybackSpeedLabel, includeSelectedPosition, state.PlaybackPosition));
        }

        public void Flush(TelemetrySessionState state, string selectedPlaybackSpeedLabel, bool includeSelectedPosition)
            => Save(state, selectedPlaybackSpeedLabel, includeSelectedPosition);
    }

    private sealed class WorkspaceTestContext : IDisposable
    {
        public WorkspaceTestContext(TelemetryWorkspace workspace, RecordingSessionPersistenceCoordinator sessionPersistenceCoordinator)
        {
            Workspace = workspace;
            SessionPersistenceCoordinator = sessionPersistenceCoordinator;
        }

        public TelemetryWorkspace Workspace { get; }

        public RecordingSessionPersistenceCoordinator SessionPersistenceCoordinator { get; }

        public void Dispose() => Workspace.Dispose();
    }

    private sealed record SaveCall(
        string? CurrentFilePath,
        string SelectedPlaybackSpeedLabel,
        bool IncludeSelectedPosition,
        int PlaybackPosition);
}
