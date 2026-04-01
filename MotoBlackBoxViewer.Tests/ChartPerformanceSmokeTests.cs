using MotoBlackBoxViewer.App.Controls;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.App.Services;
using MotoBlackBoxViewer.Core.Models;
using MotoBlackBoxViewer.Core.Services;

namespace MotoBlackBoxViewer.Tests;

public sealed class ChartPerformanceSmokeTests
{
    [Fact]
    public async Task LargeExampleLog_CanBeLoadedProcessedAndDownsampledForCharts()
    {
        string filePath = FindRepoFile("example_log_35000dots.csv");
        CsvTelemetryReader reader = new();
        TelemetryDataProcessor processor = new(new TelemetryAnalyzer());

        IReadOnlyList<TelemetryPoint> points = (await reader.ReadAsync(filePath)).Points;
        TelemetryVisibleData visibleData = processor.CreateVisibleData(points, 1, points.Count);

        Assert.True(points.Count >= 35000);
        Assert.Equal(points.Count, visibleData.SeriesSnapshot.SpeedSeries.Count);

        const int maxPointCount = 900;
        (IReadOnlyList<double> reduced, int? selectedIndex) = ChartDownsamplingHelper.DownsampleValues(
            visibleData.SeriesSnapshot.SpeedSeries,
            selectedIndex: points.Count / 2,
            maxPointCount: maxPointCount);

        Assert.True(reduced.Count <= maxPointCount + 3);
        Assert.NotNull(selectedIndex);
        Assert.InRange(selectedIndex.Value, 1, reduced.Count);
    }

    [Fact]
    public async Task LargeExampleLog_MultiSeriesDownsampling_KeepsSeriesAligned()
    {
        string filePath = FindRepoFile("example_log_35000dots.csv");
        CsvTelemetryReader reader = new();
        TelemetryDataProcessor processor = new(new TelemetryAnalyzer());

        IReadOnlyList<TelemetryPoint> points = (await reader.ReadAsync(filePath)).Points;
        TelemetryVisibleData visibleData = processor.CreateVisibleData(points, 1, points.Count);

        const int maxPointCount = 900;
        (IReadOnlyList<MotoBlackBoxViewer.App.Models.ChartSeriesDefinition> reduced, int? selectedIndex) =
            ChartDownsamplingHelper.DownsampleSeries(
                visibleData.SeriesSnapshot.AccelSeries,
                selectedIndex: points.Count / 3,
                maxPointCount: maxPointCount);

        Assert.Equal(3, reduced.Count);
        Assert.True(reduced.All(series => series.Values.Count <= maxPointCount + 3));
        Assert.Equal(reduced[0].Values.Count, reduced[1].Values.Count);
        Assert.Equal(reduced[1].Values.Count, reduced[2].Values.Count);
        Assert.NotNull(selectedIndex);
        Assert.InRange(selectedIndex.Value, 1, reduced[0].Values.Count);
    }

    private static string FindRepoFile(string fileName)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate repo file '{fileName}'.");
    }
}
