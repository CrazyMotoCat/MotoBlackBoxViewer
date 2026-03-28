namespace MotoBlackBoxViewer.App.Controls;

internal readonly record struct ChartRange(double Min, double Max)
{
    public double Span => Max - Min;

    public static ChartRange FromSeries(IReadOnlyList<double> values)
    {
        double min = double.MaxValue;
        double max = double.MinValue;

        for (int i = 0; i < values.Count; i++)
        {
            double value = values[i];
            if (value < min)
                min = value;

            if (value > max)
                max = value;
        }

        return Normalize(min, max);
    }

    public static ChartRange FromSeriesSet(IReadOnlyList<Models.ChartSeriesDefinition> seriesSet)
    {
        double min = double.MaxValue;
        double max = double.MinValue;

        for (int i = 0; i < seriesSet.Count; i++)
        {
            IReadOnlyList<double> values = seriesSet[i].Values;
            for (int j = 0; j < values.Count; j++)
            {
                double value = values[j];
                if (value < min)
                    min = value;

                if (value > max)
                    max = value;
            }
        }

        return Normalize(min, max);
    }

    private static ChartRange Normalize(double min, double max)
    {
        if (Math.Abs(max - min) < 0.0001)
            max = min + 1;

        return new ChartRange(min, max);
    }
}
