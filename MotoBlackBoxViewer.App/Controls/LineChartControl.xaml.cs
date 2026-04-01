using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace MotoBlackBoxViewer.App.Controls;

public partial class LineChartControl : UserControl
{
    private const double PointsPerPixel = 2d;
    private bool _redrawQueued;
    private RenderState _lastRenderState;

    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values),
        typeof(IReadOnlyList<double>),
        typeof(LineChartControl),
        new PropertyMetadata(null, OnChartPropertyChanged));

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex),
        typeof(int?),
        typeof(LineChartControl),
        new PropertyMetadata(null, OnChartPropertyChanged));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit),
        typeof(string),
        typeof(LineChartControl),
        new PropertyMetadata(string.Empty, OnChartPropertyChanged));

    public static readonly DependencyProperty SeriesLabelProperty = DependencyProperty.Register(
        nameof(SeriesLabel),
        typeof(string),
        typeof(LineChartControl),
        new PropertyMetadata(string.Empty, OnChartPropertyChanged));

    public static readonly DependencyProperty LineColorHexProperty = DependencyProperty.Register(
        nameof(LineColorHex),
        typeof(string),
        typeof(LineChartControl),
        new PropertyMetadata("#38BDF8", OnChartPropertyChanged));

    public static readonly DependencyProperty WindowRadiusProperty = DependencyProperty.Register(
        nameof(WindowRadius),
        typeof(int),
        typeof(LineChartControl),
        new PropertyMetadata(0, OnChartPropertyChanged));

    public LineChartControl()
    {
        InitializeComponent();
    }

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
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

    public string SeriesLabel
    {
        get => (string)GetValue(SeriesLabelProperty);
        set => SetValue(SeriesLabelProperty, value);
    }

    public string LineColorHex
    {
        get => (string)GetValue(LineColorHexProperty);
        set => SetValue(LineColorHexProperty, value);
    }

    public int WindowRadius
    {
        get => (int)GetValue(WindowRadiusProperty);
        set => SetValue(WindowRadiusProperty, value);
    }

    private static void OnChartPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((LineChartControl)d).ScheduleRedraw();

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
        var values = Values ?? Array.Empty<double>();
        RenderState nextState = new(
            RuntimeHelpers.GetHashCode(values),
            SelectedIndex,
            WindowRadius,
            Unit,
            SeriesLabel,
            LineColorHex,
            ChartCanvas.ActualWidth,
            ChartCanvas.ActualHeight);
        if (Equals(_lastRenderState, nextState))
            return;

        (IReadOnlyList<double> windowedValues, int? windowedSelectedIndex) = ChartViewportHelper.SliceValues(values, SelectedIndex, WindowRadius);
        int maxRenderablePointCount = GetMaxRenderablePointCount();
        (IReadOnlyList<double> renderValues, int? renderSelectedIndex) = ChartDownsamplingHelper.DownsampleValues(
            windowedValues,
            windowedSelectedIndex,
            maxRenderablePointCount);

        Stopwatch? redrawStopwatch = ChartPerformanceDiagnostics.HasActiveListeners
            ? Stopwatch.StartNew()
            : null;
        ChartRenderHelper.DrawSingleSeries(ChartCanvas, renderValues, Unit, renderSelectedIndex, LineColorHex, SeriesLabel);
        if (redrawStopwatch is not null)
        {
            redrawStopwatch.Stop();
            ChartPerformanceDiagnostics.Report(
                operation: "RedrawSingleSeries",
                inputPointCount: renderValues.Count,
                outputPointCount: renderValues.Count,
                elapsed: redrawStopwatch.Elapsed,
                detail: $"selected={(renderSelectedIndex.HasValue ? renderSelectedIndex.Value : 0)}; canvas={ChartCanvas.ActualWidth:F0}x{ChartCanvas.ActualHeight:F0}");
        }

        _lastRenderState = nextState;
    }

    private int GetMaxRenderablePointCount()
    {
        double width = Math.Max(1, ChartCanvas.ActualWidth);
        return Math.Max(64, (int)Math.Ceiling(width * PointsPerPixel));
    }

    private readonly record struct RenderState(
        int ValuesIdentity,
        int? SelectedIndex,
        int WindowRadius,
        string Unit,
        string SeriesLabel,
        string LineColorHex,
        double Width,
        double Height);
}
