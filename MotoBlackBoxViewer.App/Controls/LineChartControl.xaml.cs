using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MotoBlackBoxViewer.App.Controls;

public partial class LineChartControl : UserControl
{
    private bool _redrawQueued;

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
        (IReadOnlyList<double> windowedValues, int? windowedSelectedIndex) = ChartViewportHelper.SliceValues(values, SelectedIndex, WindowRadius);
        ChartRenderHelper.DrawSingleSeries(ChartCanvas, windowedValues, Unit, windowedSelectedIndex, LineColorHex, SeriesLabel);
    }
}
