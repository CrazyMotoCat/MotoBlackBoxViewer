using System.Globalization;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using MotoBlackBoxViewer.App.Models;

namespace MotoBlackBoxViewer.App.Controls;

internal static class ChartRenderHelper
{

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

        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;
        var palette = CreatePalette(colorHex);
        (double min, double max) = GetRange(values);

        DrawAxes(canvas, width, height, min, max, unit, values.Count, palette);
        canvas.Children.Add(CreatePolyline(values, width, height, min, max, palette.LineBrush));
        DrawSelectedPoint(canvas, values, width, height, min, max, selectedIndex, unit, palette.SelectedBrush, label);
    }

    public static void DrawMultiSeries(Canvas canvas, IReadOnlyList<ChartSeriesDefinition> seriesSet, string unit, int? selectedIndex)
    {
        canvas.Children.Clear();

        if (seriesSet.Count == 0 || seriesSet.All(s => s.Values.Count == 0) || canvas.ActualWidth < 10 || canvas.ActualHeight < 10)
            return;

        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;
        var primaryPalette = CreatePalette("#38BDF8");

        var allValues = seriesSet.SelectMany(s => s.Values).ToArray();
        double min = allValues.Min();
        double max = allValues.Max();
        if (Math.Abs(max - min) < 0.0001)
            max = min + 1;

        int pointCount = seriesSet.Max(s => s.Values.Count);
        DrawAxes(canvas, width, height, min, max, unit, pointCount, primaryPalette);

        for (int i = 0; i < seriesSet.Count; i++)
        {
            var series = seriesSet[i];
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(series.ColorHex));
            canvas.Children.Add(CreatePolyline(series.Values, width, height, min, max, brush));
            AddLegend(canvas, 50 + (i * 110), 4, brush, series.Label);
        }

        if (!selectedIndex.HasValue)
            return;

        int zeroBased = Math.Clamp(selectedIndex.Value - 1, 0, pointCount - 1);
        DrawSelectionGuide(canvas, width, height, zeroBased, pointCount, primaryPalette.SelectedBrush);

        double labelY = height - 24;
        for (int i = 0; i < seriesSet.Count; i++)
        {
            var series = seriesSet[i];
            if (zeroBased >= series.Values.Count)
                continue;

            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(series.ColorHex));
            AddLabel(
                canvas,
                $"{series.Label}: {series.Values[zeroBased].ToString("F2", CultureInfo.InvariantCulture)} {unit}",
                50 + (i * 150),
                labelY,
                brush);
        }
    }

    private static void DrawAxes(Canvas canvas, double width, double height, double min, double max, string unit, int pointCount, ChartPalette palette)
    {
        const double marginLeft = 42;
        const double marginRight = 12;
        const double marginTop = 24;
        const double marginBottom = 28;

        double plotWidth = Math.Max(1, width - marginLeft - marginRight);
        double plotHeight = Math.Max(1, height - marginTop - marginBottom);

        for (int i = 0; i < 5; i++)
        {
            double y = marginTop + (plotHeight / 4d) * i;
            canvas.Children.Add(new Line
            {
                X1 = marginLeft,
                X2 = marginLeft + plotWidth,
                Y1 = y,
                Y2 = y,
                Stroke = palette.GridBrush,
                StrokeThickness = 1
            });
        }

        canvas.Children.Add(new Line
        {
            X1 = marginLeft,
            X2 = marginLeft,
            Y1 = marginTop,
            Y2 = marginTop + plotHeight,
            Stroke = palette.AxisBrush,
            StrokeThickness = 1.2
        });

        canvas.Children.Add(new Line
        {
            X1 = marginLeft,
            X2 = marginLeft + plotWidth,
            Y1 = marginTop + plotHeight,
            Y2 = marginTop + plotHeight,
            Stroke = palette.AxisBrush,
            StrokeThickness = 1.2
        });

        AddLabel(canvas, $"max: {max.ToString("F1", CultureInfo.InvariantCulture)} {unit}", marginLeft, 0, palette.TextBrush);
        AddLabel(canvas, $"min: {min.ToString("F1", CultureInfo.InvariantCulture)} {unit}", marginLeft + 170, 0, palette.TextBrush);
        AddLabel(canvas, $"точек: {pointCount}", Math.Max(marginLeft, width - 110), 0, palette.TextBrush);
        AddLabel(canvas, min.ToString("F1", CultureInfo.InvariantCulture), 4, marginTop + plotHeight - 12, palette.TextBrush);
        AddLabel(canvas, max.ToString("F1", CultureInfo.InvariantCulture), 4, marginTop - 8, palette.TextBrush);
    }

    private static Polyline CreatePolyline(IReadOnlyList<double> values, double width, double height, double min, double max, Brush lineBrush)
    {
        const double marginLeft = 42;
        const double marginRight = 12;
        const double marginTop = 24;
        const double marginBottom = 28;

        double plotWidth = Math.Max(1, width - marginLeft - marginRight);
        double plotHeight = Math.Max(1, height - marginTop - marginBottom);
        var polyline = new Polyline
        {
            Stroke = lineBrush,
            StrokeThickness = 2
        };

        for (int i = 0; i < values.Count; i++)
        {
            double x = marginLeft + (values.Count == 1 ? plotWidth / 2 : plotWidth * i / (values.Count - 1d));
            double normalized = (values[i] - min) / (max - min);
            double y = marginTop + plotHeight - normalized * plotHeight;
            polyline.Points.Add(new System.Windows.Point(x, y));
        }

        return polyline;
    }

    private static void DrawSelectedPoint(Canvas canvas, IReadOnlyList<double> values, double width, double height, double min, double max, int? selectedIndex, string unit, Brush selectedBrush, string label)
    {
        if (!selectedIndex.HasValue || values.Count == 0)
            return;

        const double marginLeft = 42;
        const double marginRight = 12;
        const double marginTop = 24;
        const double marginBottom = 28;

        double plotWidth = Math.Max(1, width - marginLeft - marginRight);
        double plotHeight = Math.Max(1, height - marginTop - marginBottom);

        int zeroBased = Math.Clamp(selectedIndex.Value - 1, 0, values.Count - 1);
        double x = marginLeft + (values.Count == 1 ? plotWidth / 2 : plotWidth * zeroBased / (values.Count - 1d));
        double normalized = (values[zeroBased] - min) / (max - min);
        double y = marginTop + plotHeight - normalized * plotHeight;

        canvas.Children.Add(new Line
        {
            X1 = x,
            X2 = x,
            Y1 = marginTop,
            Y2 = marginTop + plotHeight,
            Stroke = selectedBrush,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection(new[] { 4d, 3d })
        });

        var marker = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = selectedBrush,
            Stroke = Brushes.White,
            StrokeThickness = 1.5
        };

        Canvas.SetLeft(marker, x - 5);
        Canvas.SetTop(marker, y - 5);
        canvas.Children.Add(marker);

        AddLabel(
            canvas,
            $"{label}: #{selectedIndex.Value} = {values[zeroBased].ToString("F1", CultureInfo.InvariantCulture)} {unit}",
            marginLeft,
            height - 24,
            selectedBrush);
    }

    private static void DrawSelectionGuide(Canvas canvas, double width, double height, int zeroBasedIndex, int pointCount, Brush selectedBrush)
    {
        const double marginLeft = 42;
        const double marginRight = 12;
        const double marginTop = 24;
        const double marginBottom = 28;

        double plotWidth = Math.Max(1, width - marginLeft - marginRight);
        double plotHeight = Math.Max(1, height - marginTop - marginBottom);
        double x = marginLeft + (pointCount == 1 ? plotWidth / 2 : plotWidth * zeroBasedIndex / (pointCount - 1d));

        canvas.Children.Add(new Line
        {
            X1 = x,
            X2 = x,
            Y1 = marginTop,
            Y2 = marginTop + plotHeight,
            Stroke = selectedBrush,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection(new[] { 4d, 3d })
        });
    }

    private static (double Min, double Max) GetRange(IReadOnlyList<double> values)
    {
        double min = values.Min();
        double max = values.Max();
        if (Math.Abs(max - min) < 0.0001)
            max = min + 1;

        return (min, max);
    }

    private static ChartPalette CreatePalette(string lineColorHex)
    {
        return new ChartPalette(
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(lineColorHex)),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F97316")));
    }

    private static void AddLegend(Canvas canvas, double x, double y, Brush brush, string text)
    {
        var swatch = new Rectangle
        {
            Width = 16,
            Height = 4,
            Fill = brush
        };

        Canvas.SetLeft(swatch, x);
        Canvas.SetTop(swatch, y + 7);
        canvas.Children.Add(swatch);

        AddLabel(canvas, text, x + 22, y, brush);
    }

    private static void AddLabel(Canvas canvas, string text, double x, double y, Brush brush)
    {
        var tb = new TextBlock
        {
            Text = text,
            Foreground = brush,
            FontSize = 12
        };

        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        canvas.Children.Add(tb);
    }
}
