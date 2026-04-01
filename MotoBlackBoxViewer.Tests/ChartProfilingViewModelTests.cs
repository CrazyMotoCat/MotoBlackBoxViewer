using MotoBlackBoxViewer.App.Controls;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.App.ViewModels;

namespace MotoBlackBoxViewer.Tests;

public sealed class ChartProfilingViewModelTests
{
    [Fact]
    public void IsEnabled_WhenPerformanceEventsArrive_AggregatesByPipelineStage()
    {
        using TelemetryChartProfilingViewModel profiling = new(new TelemetrySessionState());
        profiling.IsEnabled = true;

        ChartPerformanceDiagnostics.Report("CreateVisibleData", 7000, 6801, TimeSpan.FromMilliseconds(11));
        ChartPerformanceDiagnostics.Report("SliceValues", 6801, 401, TimeSpan.FromMilliseconds(2.5), "radius=200; sliced=true");
        ChartPerformanceDiagnostics.Report("SliceSeries", 6801, 401, TimeSpan.FromMilliseconds(3.5), "seriesCount=3");
        ChartPerformanceDiagnostics.Report("DownsampleValues", 401, 240, TimeSpan.FromMilliseconds(4), "budget=240");
        ChartPerformanceDiagnostics.Report("RedrawSingleSeries", 240, 240, TimeSpan.FromMilliseconds(6), "canvas=600x200");

        Assert.True(profiling.HasData);
        Assert.Equal(4, profiling.Rows.Count);

        ChartProfilingOperationSummary slicing = Assert.Single(profiling.Rows.Where(row => row.Stage == "Chart slicing"));
        Assert.Equal(2, slicing.Samples);
        Assert.Equal(6d, slicing.TotalMilliseconds, 3);
        Assert.Equal("SliceSeries", slicing.LastOperation);
    }

    [Fact]
    public void IsEnabled_WhenDisabled_ClearsCurrentSessionAndStopsCollecting()
    {
        using TelemetryChartProfilingViewModel profiling = new(new TelemetrySessionState());
        profiling.IsEnabled = true;
        ChartPerformanceDiagnostics.Report("CreateVisibleData", 100, 100, TimeSpan.FromMilliseconds(1));

        profiling.IsEnabled = false;
        ChartPerformanceDiagnostics.Report("CreateVisibleData", 100, 100, TimeSpan.FromMilliseconds(1));

        Assert.False(profiling.HasData);
        Assert.Empty(profiling.Rows);
        Assert.Equal("Режим выключен.", profiling.SummaryText);
    }
}
