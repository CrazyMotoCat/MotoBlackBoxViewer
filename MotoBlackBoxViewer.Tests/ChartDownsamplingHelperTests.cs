using MotoBlackBoxViewer.App.Controls;
using MotoBlackBoxViewer.App.Models;

namespace MotoBlackBoxViewer.Tests;

public sealed class ChartDownsamplingHelperTests
{
    [Fact]
    public void DownsampleValues_WhenSeriesFitsBudget_ReturnsOriginalValues()
    {
        double[] values = [1, 2, 3, 4];

        (IReadOnlyList<double> reduced, int? selectedIndex) = ChartDownsamplingHelper.DownsampleValues(values, selectedIndex: 3, maxPointCount: 8);

        Assert.Same(values, reduced);
        Assert.Equal(3, selectedIndex);
    }

    [Fact]
    public void DownsampleValues_PreservesEndpointsAndSelectedPoint()
    {
        double[] values = Enumerable.Range(1, 100).Select(static value => (double)value).ToArray();
        const int maxPointCount = 10;

        (IReadOnlyList<double> reduced, int? selectedIndex) = ChartDownsamplingHelper.DownsampleValues(values, selectedIndex: 50, maxPointCount: maxPointCount);

        Assert.True(reduced.Count <= maxPointCount + 3);
        Assert.Equal(1d, reduced[0]);
        Assert.Equal(100d, reduced[^1]);
        Assert.NotNull(selectedIndex);
        Assert.Equal(50d, reduced[selectedIndex.Value - 1]);
    }

    [Fact]
    public void DownsampleValues_PreservesSharpInteriorSpike()
    {
        double[] values = Enumerable.Repeat(0d, 120).ToArray();
        values[57] = 999d;

        (IReadOnlyList<double> reduced, int? selectedIndex) = ChartDownsamplingHelper.DownsampleValues(values, selectedIndex: null, maxPointCount: 12);

        Assert.Contains(999d, reduced);
        Assert.Null(selectedIndex);
    }

    [Fact]
    public void DownsampleSeries_PreservesSharedIndexingAcrossSeries()
    {
        ChartSeriesDefinition[] series =
        [
            new("A", Enumerable.Range(1, 100).Select(static value => (double)value).ToArray(), "#111111"),
            new("B", Enumerable.Range(201, 100).Select(static value => (double)value).ToArray(), "#222222")
        ];

        (IReadOnlyList<ChartSeriesDefinition> reduced, int? selectedIndex) = ChartDownsamplingHelper.DownsampleSeries(series, selectedIndex: 60, maxPointCount: 12);

        Assert.Equal(2, reduced.Count);
        Assert.Equal(reduced[0].Values.Count, reduced[1].Values.Count);
        Assert.NotNull(selectedIndex);
        Assert.Equal(60d, reduced[0].Values[selectedIndex.Value - 1]);
        Assert.Equal(260d, reduced[1].Values[selectedIndex.Value - 1]);
    }

    [Fact]
    public void DownsampleSeries_PreservesEnvelopeExtremaFromDifferentSeries()
    {
        double[] a = Enumerable.Repeat(0d, 120).ToArray();
        double[] b = Enumerable.Repeat(10d, 120).ToArray();
        a[31] = -500d;
        b[78] = 700d;

        ChartSeriesDefinition[] series =
        [
            new("A", a, "#111111"),
            new("B", b, "#222222")
        ];

        (IReadOnlyList<ChartSeriesDefinition> reduced, _) = ChartDownsamplingHelper.DownsampleSeries(series, selectedIndex: null, maxPointCount: 12);

        Assert.Contains(-500d, reduced[0].Values);
        Assert.Contains(700d, reduced[1].Values);
    }
}
