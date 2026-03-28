using System.Windows;
using System.Windows.Controls;

namespace MotoBlackBoxViewer.App.Controls;

public partial class LineChartControl : UserControl
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values),
        typeof(IEnumerable<double>),
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

    public LineChartControl()
    {
        InitializeComponent();
    }

    public IEnumerable<double>? Values
    {
        get => (IEnumerable<double>?)GetValue(ValuesProperty);
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

    private static void OnChartPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((LineChartControl)d).Redraw();

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        var values = Values?.ToArray() ?? Array.Empty<double>();
        ChartRenderHelper.DrawSingleSeries(ChartCanvas, values, Unit, SelectedIndex, LineColorHex, SeriesLabel);
    }
}
