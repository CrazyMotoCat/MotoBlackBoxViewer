namespace MotoBlackBoxViewer.App.Controls;

internal readonly record struct ChartLayout(
    double Width,
    double Height,
    double MarginLeft,
    double MarginRight,
    double MarginTop,
    double MarginBottom)
{
    public static ChartLayout Create(double width, double height)
        => new(width, height, 42, 12, 24, 28);

    public double PlotWidth => Math.Max(1, Width - MarginLeft - MarginRight);

    public double PlotHeight => Math.Max(1, Height - MarginTop - MarginBottom);

    public double Bottom => MarginTop + PlotHeight;

    public double GetX(int index, int pointCount)
        => MarginLeft + (pointCount == 1 ? PlotWidth / 2 : PlotWidth * index / (pointCount - 1d));

    public double GetY(double value, ChartRange range)
    {
        double normalized = (value - range.Min) / range.Span;
        return MarginTop + PlotHeight - normalized * PlotHeight;
    }
}
