using MotoBlackBoxViewer.App.Models;
using System.Diagnostics;

namespace MotoBlackBoxViewer.App.Controls;

internal static class ChartViewportHelper
{
    public static (IReadOnlyList<double> Values, int? SelectedIndex) SliceValues(
        IReadOnlyList<double> values,
        int? selectedIndex,
        int windowRadius)
    {
        Stopwatch? stopwatch = ChartPerformanceDiagnostics.HasActiveListeners
            ? Stopwatch.StartNew()
            : null;

        IReadOnlyList<double> resultValues = values;
        int? resultSelectedIndex = selectedIndex;
        bool sliced = false;

        if (values.Count != 0 && selectedIndex.HasValue && windowRadius > 0)
        {
            int zeroBasedSelected = Math.Clamp(selectedIndex.Value - 1, 0, values.Count - 1);
            int start = Math.Max(0, zeroBasedSelected - windowRadius);
            int end = Math.Min(values.Count - 1, zeroBasedSelected + windowRadius);

            if (start != 0 || end != values.Count - 1)
            {
                int length = end - start + 1;
                double[] slice = new double[length];
                for (int i = 0; i < length; i++)
                    slice[i] = values[start + i];

                resultValues = slice;
                resultSelectedIndex = zeroBasedSelected - start + 1;
                sliced = true;
            }
        }

        if (stopwatch is not null)
        {
            stopwatch.Stop();
            ChartPerformanceDiagnostics.Report(
                operation: "SliceValues",
                inputPointCount: values.Count,
                outputPointCount: resultValues.Count,
                elapsed: stopwatch.Elapsed,
                detail: $"selected={(selectedIndex.HasValue ? selectedIndex.Value : 0)}; radius={windowRadius}; sliced={sliced}");
        }

        return (resultValues, resultSelectedIndex);
    }

    public static (IReadOnlyList<ChartSeriesDefinition> Series, int? SelectedIndex) SliceSeries(
        IReadOnlyList<ChartSeriesDefinition> seriesSet,
        int? selectedIndex,
        int windowRadius)
    {
        Stopwatch? stopwatch = ChartPerformanceDiagnostics.HasActiveListeners
            ? Stopwatch.StartNew()
            : null;

        IReadOnlyList<ChartSeriesDefinition> resultSeries = seriesSet;
        int? resultSelectedIndex = selectedIndex;
        bool sliced = false;

        if (seriesSet.Count != 0 && selectedIndex.HasValue && windowRadius > 0)
        {
            int maxCount = seriesSet.Max(series => series.Values.Count);
            if (maxCount > 0)
            {
                int zeroBasedSelected = Math.Clamp(selectedIndex.Value - 1, 0, maxCount - 1);
                int start = Math.Max(0, zeroBasedSelected - windowRadius);
                int end = Math.Min(maxCount - 1, zeroBasedSelected + windowRadius);

                if (start != 0 || end != maxCount - 1)
                {
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

                    resultSeries = slicedSeries;
                    resultSelectedIndex = zeroBasedSelected - start + 1;
                    sliced = true;
                }
            }
        }

        if (stopwatch is not null)
        {
            stopwatch.Stop();
            int outputPointCount = resultSeries.Count == 0 ? 0 : resultSeries.Max(series => series.Values.Count);
            ChartPerformanceDiagnostics.Report(
                operation: "SliceSeries",
                inputPointCount: seriesSet.Count == 0 ? 0 : seriesSet.Max(series => series.Values.Count),
                outputPointCount: outputPointCount,
                elapsed: stopwatch.Elapsed,
                detail: $"seriesCount={seriesSet.Count}; selected={(selectedIndex.HasValue ? selectedIndex.Value : 0)}; radius={windowRadius}; sliced={sliced}");
        }

        return (resultSeries, resultSelectedIndex);
    }
}
