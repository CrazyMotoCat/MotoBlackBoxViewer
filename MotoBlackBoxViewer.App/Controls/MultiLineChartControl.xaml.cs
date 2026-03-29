using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Runtime.CompilerServices;

using MotoBlackBoxViewer.App.Models;

namespace MotoBlackBoxViewer.App.Controls;

public partial class MultiLineChartControl : UserControl
{
    private const double PointsPerPixel = 2d;
    private bool _redrawQueued;
    private RenderState _lastRenderState;

    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
        nameof(Series),
        typeof(IReadOnlyList<ChartSeriesDefinition>),
        typeof(MultiLineChartControl),
        new PropertyMetadata(null, OnChartPropertyChanged));

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex),
        typeof(int?),
        typeof(MultiLineChartControl),
        new PropertyMetadata(null, OnChartPropertyChanged));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit),
        typeof(string),
        typeof(MultiLineChartControl),
        new PropertyMetadata(string.Empty, OnChartPropertyChanged));

    public static readonly DependencyProperty WindowRadiusProperty = DependencyProperty.Register(
        nameof(WindowRadius),
        typeof(int),
        typeof(MultiLineChartControl),
        new PropertyMetadata(0, OnChartPropertyChanged));

    public MultiLineChartControl()
    {
        InitializeComponent();
    }

    public IReadOnlyList<ChartSeriesDefinition>? Series
    {
        get => (IReadOnlyList<ChartSeriesDefinition>?)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public int? SelectedIndex
    {
        get => (int?)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public int WindowRadius
    {
        get => (int)GetValue(WindowRadiusProperty);
        set => SetValue(WindowRadiusProperty, value);
    }

    private static void OnChartPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MultiLineChartControl)d).ScheduleRedraw();

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => ScheduleRedraw();

    private void ScheduleRedraw()
    {
        if (_redrawQueued)
            return;

        _redrawQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            _redrawQueued = false;
            Redraw();
        }));
    }

    private void Redraw()
    {
        var series = Series ?? Array.Empty<ChartSeriesDefinition>();
        RenderState nextState = new(
            RuntimeHelpers.GetHashCode(series),
            SelectedIndex,
            WindowRadius,
            Unit,
            ChartCanvas.ActualWidth,
            ChartCanvas.ActualHeight);
        if (Equals(_lastRenderState, nextState))
            return;

        (IReadOnlyList<ChartSeriesDefinition> windowedSeries, int? windowedSelectedIndex) = ChartViewportHelper.SliceSeries(series, SelectedIndex, WindowRadius);
        int maxRenderablePointCount = GetMaxRenderablePointCount();
        (IReadOnlyList<ChartSeriesDefinition> renderSeries, int? renderSelectedIndex) = ChartDownsamplingHelper.DownsampleSeries(
            windowedSeries,
            windowedSelectedIndex,
            maxRenderablePointCount);
        ChartRenderHelper.DrawMultiSeries(ChartCanvas, renderSeries, Unit, renderSelectedIndex);
        _lastRenderState = nextState;
    }

    private int GetMaxRenderablePointCount()
    {
        double width = Math.Max(1, ChartCanvas.ActualWidth);
        return Math.Max(64, (int)Math.Ceiling(width * PointsPerPixel));
    }

    private readonly record struct RenderState(
        int SeriesIdentity,
        int? SelectedIndex,
        int WindowRadius,
        string Unit,
        double Width,
        double Height);
}
