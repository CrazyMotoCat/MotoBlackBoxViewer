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
        Assert.True(propertyChangedCount > 0);

        selection.Dispose();
        propertyChangedCount = 0;

        data.FilterStartIndex = 1;

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

    private static TelemetryPoint CreatePoint(int index, double speedKmh)
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
            DistanceFromStartMeters = index * 100
        };
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
            => $"[{string.Join(",", points.Select(p => p.Index))}]";

        public string ExportHtml(IReadOnlyList<TelemetryPoint> points, string baseDirectory)
            => Path.Combine(baseDirectory, "map.html");

        public void OpenInBrowser(string htmlPath)
        {
        }
    }
}
