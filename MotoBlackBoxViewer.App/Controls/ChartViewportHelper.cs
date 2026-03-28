using MotoBlackBoxViewer.App.Models;

namespace MotoBlackBoxViewer.App.Controls;

internal static class ChartViewportHelper
{
    public static (IReadOnlyList<double> Values, int? SelectedIndex) SliceValues(
        IReadOnlyList<double> values,
        int? selectedIndex,
        int windowRadius)
    {
        if (values.Count == 0 || !selectedIndex.HasValue || windowRadius <= 0)
            return (values, selectedIndex);

        int zeroBasedSelected = Math.Clamp(selectedIndex.Value - 1, 0, values.Count - 1);
        int start = Math.Max(0, zeroBasedSelected - windowRadius);
        int end = Math.Min(values.Count - 1, zeroBasedSelected + windowRadius);

        if (start == 0 && end == values.Count - 1)
            return (values, selectedIndex);

        int length = end - start + 1;
        double[] slice = new double[length];
        for (int i = 0; i < length; i++)
            slice[i] = values[start + i];

        return (slice, zeroBasedSelected - start + 1);
    }

    public static (IReadOnlyList<ChartSeriesDefinition> Series, int? SelectedIndex) SliceSeries(
        IReadOnlyList<ChartSeriesDefinition> seriesSet,
        int? selectedIndex,
        int windowRadius)
    {
        if (seriesSet.Count == 0 || !selectedIndex.HasValue || windowRadius <= 0)
            return (seriesSet, selectedIndex);

        int maxCount = seriesSet.Max(series => series.Values.Count);
        if (maxCount == 0)
            return (seriesSet, selectedIndex);

        int zeroBasedSelected = Math.Clamp(selectedIndex.Value - 1, 0, maxCount - 1);
        int start = Math.Max(0, zeroBasedSelected - windowRadius);
        int end = Math.Min(maxCount - 1, zeroBasedSelected + windowRadius);

        if (start == 0 && end == maxCount - 1)
            return (seriesSet, selectedIndex);

        int length = end - start + 1;
        ChartSeriesDefinition[] slicedSeries = new ChartSeriesDefinition[seriesSet.Count];

        for (int i = 0; i < seriesSet.Count; i++)
        {
            ChartSeriesDefinition series = seriesSet[i];
            int seriesLength = Math.Max(0, Math.Min(length, series.Values.Count - start));
            double[] slice = new double[seriesLength];
            for (int j = 0; j < seriesLength; j++)
                slice[j] = series.Values[start + j];

            slicedSeries[i] = new ChartSeriesDefinition(series.Label, slice, series.ColorHex);
        }

        return (slicedSeries, zeroBasedSelected - start + 1);
    }
}
