using MotoBlackBoxViewer.App.Controls;
using MotoBlackBoxViewer.App.Models;

namespace MotoBlackBoxViewer.Tests;

public sealed class ChartViewportHelperTests
{
    [Fact]
    public void SliceValues_ReturnsWindowAroundSelectedPoint()
    {
        double[] values = Enumerable.Range(1, 10).Select(static value => (double)value).ToArray();

        (IReadOnlyList<double> slice, int? selectedIndex) = ChartViewportHelper.SliceValues(values, selectedIndex: 6, windowRadius: 2);

        Assert.Equal([4d, 5d, 6d, 7d, 8d], slice);
        Assert.Equal(3, selectedIndex);
    }

    [Fact]
    public void SliceSeries_ReturnsWindowAroundSelectedPoint()
    {
        ChartSeriesDefinition[] series =
        [
            new("A", Enumerable.Range(1, 8).Select(static value => (double)value).ToArray(), "#111111"),
            new("B", Enumerable.Range(11, 8).Select(static value => (double)value).ToArray(), "#222222")
        ];

        (IReadOnlyList<ChartSeriesDefinition> slice, int? selectedIndex) = ChartViewportHelper.SliceSeries(series, selectedIndex: 4, windowRadius: 1);

        Assert.Equal([3d, 4d, 5d], slice[0].Values);
        Assert.Equal([13d, 14d, 15d], slice[1].Values);
        Assert.Equal(2, selectedIndex);
    }
}
