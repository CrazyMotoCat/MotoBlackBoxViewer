using MotoBlackBoxViewer.App.Models;
using System.Diagnostics;

namespace MotoBlackBoxViewer.App.Controls;

internal static class ChartDownsamplingHelper
{
    public static (IReadOnlyList<double> Values, int? SelectedIndex) DownsampleValues(
        IReadOnlyList<double> values,
        int? selectedIndex,
        int maxPointCount)
    {
        if (values.Count <= 2 || maxPointCount <= 0 || values.Count <= maxPointCount)
            return (values, selectedIndex);

        Stopwatch stopwatch = Stopwatch.StartNew();

        int[] sampledIndices = BuildSampledIndices(
            values.Count,
            selectedIndex,
            maxPointCount,
            (start, end, indices) => AddBucketExtrema(values, start, end, indices));

        double[] reducedValues = new double[sampledIndices.Length];
        int? reducedSelectedIndex = null;

        for (int i = 0; i < sampledIndices.Length; i++)
        {
            int sourceIndex = sampledIndices[i];
            reducedValues[i] = values[sourceIndex];

            if (selectedIndex.HasValue && sourceIndex == selectedIndex.Value - 1)
                reducedSelectedIndex = i + 1;
        }

        stopwatch.Stop();
        ChartPerformanceDiagnostics.Report(
            operation: "DownsampleValues",
            inputPointCount: values.Count,
            outputPointCount: reducedValues.Length,
            elapsed: stopwatch.Elapsed,
            detail: $"selected={(selectedIndex.HasValue ? selectedIndex.Value : 0)}; budget={maxPointCount}");

        return (reducedValues, reducedSelectedIndex);
    }

    public static (IReadOnlyList<ChartSeriesDefinition> Series, int? SelectedIndex) DownsampleSeries(
        IReadOnlyList<ChartSeriesDefinition> seriesSet,
        int? selectedIndex,
        int maxPointCount)
    {
        if (seriesSet.Count == 0 || maxPointCount <= 0)
            return (seriesSet, selectedIndex);

        int maxSeriesCount = seriesSet.Max(series => series.Values.Count);
        if (maxSeriesCount <= 2 || maxSeriesCount <= maxPointCount)
            return (seriesSet, selectedIndex);

        Stopwatch stopwatch = Stopwatch.StartNew();

        int[] sampledIndices = BuildSampledIndices(
            maxSeriesCount,
            selectedIndex,
            maxPointCount,
            (start, end, indices) => AddBucketEnvelopeExtrema(seriesSet, start, end, indices));

        ChartSeriesDefinition[] reducedSeries = new ChartSeriesDefinition[seriesSet.Count];
        int? reducedSelectedIndex = null;

        for (int i = 0; i < seriesSet.Count; i++)
        {
            ChartSeriesDefinition series = seriesSet[i];
            List<double> reducedValues = new(sampledIndices.Length);

            for (int j = 0; j < sampledIndices.Length; j++)
            {
                int sourceIndex = sampledIndices[j];
                if (sourceIndex >= series.Values.Count)
                    continue;

                reducedValues.Add(series.Values[sourceIndex]);

                if (reducedSelectedIndex is null
                    && selectedIndex.HasValue
                    && sourceIndex == selectedIndex.Value - 1)
                {
                    reducedSelectedIndex = reducedValues.Count;
                }
            }

            reducedSeries[i] = new ChartSeriesDefinition(series.Label, reducedValues.ToArray(), series.ColorHex);
        }

        stopwatch.Stop();
        ChartPerformanceDiagnostics.Report(
            operation: "DownsampleSeries",
            inputPointCount: maxSeriesCount,
            outputPointCount: reducedSeries.Length == 0 ? 0 : reducedSeries[0].Values.Count,
            elapsed: stopwatch.Elapsed,
            detail: $"seriesCount={seriesSet.Count}; selected={(selectedIndex.HasValue ? selectedIndex.Value : 0)}; budget={maxPointCount}");

        return (reducedSeries, reducedSelectedIndex);
    }

    private static int[] BuildSampledIndices(
        int sourceCount,
        int? selectedIndex,
        int maxPointCount,
        Action<int, int, SortedSet<int>> addBucketIndices)
    {
        SortedSet<int> indices = [0, sourceCount - 1];
        if (selectedIndex.HasValue)
            indices.Add(Math.Clamp(selectedIndex.Value - 1, 0, sourceCount - 1));

        int interiorCount = Math.Max(0, sourceCount - 2);
        if (interiorCount == 0)
            return [.. indices];

        int bucketCount = Math.Max(1, maxPointCount / 2);
        int bucketSize = Math.Max(1, (int)Math.Ceiling(interiorCount / (double)bucketCount));

        for (int bucketStart = 1; bucketStart < sourceCount - 1; bucketStart += bucketSize)
        {
            int bucketEnd = Math.Min(sourceCount - 2, bucketStart + bucketSize - 1);
            addBucketIndices(bucketStart, bucketEnd, indices);
        }

        return [.. indices];
    }

    private static void AddBucketExtrema(
        IReadOnlyList<double> values,
        int bucketStart,
        int bucketEnd,
        SortedSet<int> indices)
    {
        int minIndex = bucketStart;
        int maxIndex = bucketStart;
        double minValue = values[bucketStart];
        double maxValue = values[bucketStart];

        for (int i = bucketStart + 1; i <= bucketEnd; i++)
        {
            double value = values[i];
            if (value < minValue)
            {
                minValue = value;
                minIndex = i;
            }

            if (value > maxValue)
            {
                maxValue = value;
                maxIndex = i;
            }
        }

        indices.Add(minIndex);
        indices.Add(maxIndex);
    }

    private static void AddBucketEnvelopeExtrema(
        IReadOnlyList<ChartSeriesDefinition> seriesSet,
        int bucketStart,
        int bucketEnd,
        SortedSet<int> indices)
    {
        int? minIndex = null;
        int? maxIndex = null;
        double minValue = double.MaxValue;
        double maxValue = double.MinValue;

        for (int i = bucketStart; i <= bucketEnd; i++)
        {
            for (int j = 0; j < seriesSet.Count; j++)
            {
                IReadOnlyList<double> values = seriesSet[j].Values;
                if (i >= values.Count)
                    continue;

                double value = values[i];
                if (value < minValue)
                {
                    minValue = value;
                    minIndex = i;
                }

                if (value > maxValue)
                {
                    maxValue = value;
                    maxIndex = i;
                }
            }
        }

        if (minIndex.HasValue)
            indices.Add(minIndex.Value);

        if (maxIndex.HasValue)
            indices.Add(maxIndex.Value);
    }
}
