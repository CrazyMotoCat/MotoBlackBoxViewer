using System.Windows;
using System.Windows.Controls;

using MotoBlackBoxViewer.App.Models;

namespace MotoBlackBoxViewer.App.Controls;

public partial class MultiLineChartControl : UserControl
{
    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
        nameof(Series),
        typeof(IEnumerable<ChartSeriesDefinition>),
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

    public MultiLineChartControl()
    {
        InitializeComponent();
    }

    public IEnumerable<ChartSeriesDefinition>? Series
    {
        get => (IEnumerable<ChartSeriesDefinition>?)GetValue(SeriesProperty);
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

    private static void OnChartPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MultiLineChartControl)d).Redraw();

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        var series = Series?.ToArray() ?? Array.Empty<ChartSeriesDefinition>();
        ChartRenderHelper.DrawMultiSeries(ChartCanvas, series, Unit, SelectedIndex);
    }
}
