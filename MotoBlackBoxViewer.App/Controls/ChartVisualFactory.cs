using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MotoBlackBoxViewer.App.Controls;

internal static class ChartVisualFactory
{
    public static Line CreateLine(double x1, double y1, double x2, double y2, Brush stroke, double thickness, DoubleCollection? dashArray = null)
    {
        var line = new Line
        {
            X1 = x1,
            X2 = x2,
            Y1 = y1,
            Y2 = y2,
            Stroke = stroke,
            StrokeThickness = thickness
        };

        if (dashArray is not null)
            line.StrokeDashArray = dashArray;

        return line;
    }

    public static Polyline CreatePolyline(IReadOnlyList<double> values, ChartLayout layout, ChartRange range, Brush lineBrush)
    {
        var polyline = new Polyline
        {
            Stroke = lineBrush,
            StrokeThickness = 2
        };

        for (int i = 0; i < values.Count; i++)
            polyline.Points.Add(new Point(layout.GetX(i, values.Count), layout.GetY(values[i], range)));

        return polyline;
    }

    public static Ellipse CreateMarker(Brush fill)
        => new()
        {
            Width = 10,
            Height = 10,
            Fill = fill,
            Stroke = Brushes.White,
            StrokeThickness = 1.5
        };

    public static Rectangle CreateLegendSwatch(Brush brush)
        => new()
        {
            Width = 16,
            Height = 4,
            Fill = brush
        };

    public static TextBlock CreateLabel(string text, Brush brush)
        => new()
        {
            Text = text,
            Foreground = brush,
            FontSize = 12
        };

    public static void Place(UIElement element, Canvas canvas, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        canvas.Children.Add(element);
    }
}
