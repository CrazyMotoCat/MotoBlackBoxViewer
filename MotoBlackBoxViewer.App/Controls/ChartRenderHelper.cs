using System.Globalization;
using System.Windows.Controls;
using System.Windows.Media;

using MotoBlackBoxViewer.App.Models;

namespace MotoBlackBoxViewer.App.Controls;

internal static class ChartRenderHelper
{
    private static readonly DoubleCollection SelectionDashArray = [4d, 3d];

    private sealed record ChartPalette(
        Brush GridBrush,
        Brush AxisBrush,
        Brush LineBrush,
        Brush TextBrush,
        Brush SelectedBrush);

    public static void DrawSingleSeries(Canvas canvas, IReadOnlyList<double> values, string unit, int? selectedIndex, string colorHex, string label)
    {
        canvas.Children.Clear();

        if (values.Count == 0 || canvas.ActualWidth < 10 || canvas.ActualHeight < 10)
            return;

        var layout = ChartLayout.Create(canvas.ActualWidth, canvas.ActualHeight);
        ChartRange range = ChartRange.FromSeries(values);
        ChartPalette palette = CreatePalette(colorHex);

        DrawAxes(canvas, layout, range, unit, values.Count, palette);
        canvas.Children.Add(ChartVisualFactory.CreatePolyline(values, layout, range, palette.LineBrush));
        DrawSelectedPoint(canvas, values, layout, range, selectedIndex, unit, palette.SelectedBrush, label);
    }

    public static void DrawMultiSeries(Canvas canvas, IReadOnlyList<ChartSeriesDefinition> seriesSet, string unit, int? selectedIndex)
    {
        canvas.Children.Clear();

        if (seriesSet.Count == 0 || seriesSet.All(s => s.Values.Count == 0) || canvas.ActualWidth < 10 || canvas.ActualHeight < 10)
            return;

        var layout = ChartLayout.Create(canvas.ActualWidth, canvas.ActualHeight);
        ChartRange range = ChartRange.FromSeriesSet(seriesSet);
        ChartPalette primaryPalette = CreatePalette("#C65A69");
        int pointCount = seriesSet.Max(s => s.Values.Count);

        DrawAxes(canvas, layout, range, unit, pointCount, primaryPalette);

        for (int i = 0; i < seriesSet.Count; i++)
        {
            ChartSeriesDefinition series = seriesSet[i];
            Brush brush = CreateBrush(series.ColorHex);
            canvas.Children.Add(ChartVisualFactory.CreatePolyline(series.Values, layout, range, brush));
            AddLegend(canvas, 50 + (i * 110), 4, brush, series.Label);
        }

        if (!selectedIndex.HasValue)
            return;

        int zeroBased = Math.Clamp(selectedIndex.Value - 1, 0, pointCount - 1);
        DrawSelectionGuide(canvas, layout, zeroBased, pointCount, primaryPalette.SelectedBrush);

        double labelY = layout.Height - 24;
        for (int i = 0; i < seriesSet.Count; i++)
        {
            ChartSeriesDefinition series = seriesSet[i];
            if (zeroBased >= series.Values.Count)
                continue;

            AddLabel(
                canvas,
                $"{series.Label}: {series.Values[zeroBased].ToString("F2", CultureInfo.InvariantCulture)} {unit}",
                50 + (i * 150),
                labelY,
                CreateBrush(series.ColorHex));
        }
    }

    private static void DrawAxes(Canvas canvas, ChartLayout layout, ChartRange range, string unit, int pointCount, ChartPalette palette)
    {
        for (int i = 0; i < 5; i++)
        {
            double y = layout.MarginTop + (layout.PlotHeight / 4d) * i;
            canvas.Children.Add(ChartVisualFactory.CreateLine(
                layout.MarginLeft,
                y,
                layout.MarginLeft + layout.PlotWidth,
                y,
                palette.GridBrush,
                1));
        }

        canvas.Children.Add(ChartVisualFactory.CreateLine(
            layout.MarginLeft,
            layout.MarginTop,
            layout.MarginLeft,
            layout.Bottom,
            palette.AxisBrush,
            1.2));

        canvas.Children.Add(ChartVisualFactory.CreateLine(
            layout.MarginLeft,
            layout.Bottom,
            layout.MarginLeft + layout.PlotWidth,
            layout.Bottom,
            palette.AxisBrush,
            1.2));

        AddLabel(canvas, $"max: {range.Max.ToString("F1", CultureInfo.InvariantCulture)} {unit}", layout.MarginLeft, 0, palette.TextBrush);
        AddLabel(canvas, $"min: {range.Min.ToString("F1", CultureInfo.InvariantCulture)} {unit}", layout.MarginLeft + 170, 0, palette.TextBrush);
        AddLabel(canvas, $"точек: {pointCount}", Math.Max(layout.MarginLeft, layout.Width - 110), 0, palette.TextBrush);
        AddLabel(canvas, range.Min.ToString("F1", CultureInfo.InvariantCulture), 4, layout.Bottom - 12, palette.TextBrush);
        AddLabel(canvas, range.Max.ToString("F1", CultureInfo.InvariantCulture), 4, layout.MarginTop - 8, palette.TextBrush);
    }

    private static void DrawSelectedPoint(Canvas canvas, IReadOnlyList<double> values, ChartLayout layout, ChartRange range, int? selectedIndex, string unit, Brush selectedBrush, string label)
    {
        if (!selectedIndex.HasValue || values.Count == 0)
            return;

        int zeroBased = Math.Clamp(selectedIndex.Value - 1, 0, values.Count - 1);
        double x = layout.GetX(zeroBased, values.Count);
        double y = layout.GetY(values[zeroBased], range);

        canvas.Children.Add(ChartVisualFactory.CreateLine(
            x,
            layout.MarginTop,
            x,
            layout.Bottom,
            selectedBrush,
            1.5,
            SelectionDashArray));

        ChartVisualFactory.Place(ChartVisualFactory.CreateMarker(selectedBrush), canvas, x - 5, y - 5);
        AddLabel(
            canvas,
            $"{label}: #{selectedIndex.Value} = {values[zeroBased].ToString("F1", CultureInfo.InvariantCulture)} {unit}",
            layout.MarginLeft,
            layout.Height - 24,
            selectedBrush);
    }

    private static void DrawSelectionGuide(Canvas canvas, ChartLayout layout, int zeroBasedIndex, int pointCount, Brush selectedBrush)
    {
        double x = layout.GetX(zeroBasedIndex, pointCount);
        canvas.Children.Add(ChartVisualFactory.CreateLine(
            x,
            layout.MarginTop,
            x,
            layout.Bottom,
            selectedBrush,
            1.5,
            SelectionDashArray));
    }

    private static ChartPalette CreatePalette(string lineColorHex)
        => new(
            CreateBrush("#2D3742"),
            CreateBrush("#728091"),
            CreateBrush(lineColorHex),
            CreateBrush("#D9E0E7"),
            CreateBrush("#B45768"));

    private static SolidColorBrush CreateBrush(string colorHex)
        => new((Color)ColorConverter.ConvertFromString(colorHex));

    private static void AddLegend(Canvas canvas, double x, double y, Brush brush, string text)
    {
        ChartVisualFactory.Place(ChartVisualFactory.CreateLegendSwatch(brush), canvas, x, y + 7);
        AddLabel(canvas, text, x + 22, y, brush);
    }

    private static void AddLabel(Canvas canvas, string text, double x, double y, Brush brush)
        => ChartVisualFactory.Place(ChartVisualFactory.CreateLabel(text, brush), canvas, x, y);
}
