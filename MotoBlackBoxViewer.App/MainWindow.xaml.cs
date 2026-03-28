using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using MotoBlackBoxViewer.App.ViewModels;

namespace MotoBlackBoxViewer.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly DispatcherTimer _playbackTimer;
    private bool _isMapReady;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        _playbackTimer = new DispatcherTimer();
        UpdatePlaybackTimerInterval();
        _playbackTimer.Tick += PlaybackTimer_Tick;

        Loaded += MainWindow_Loaded;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeMapAsync();
        RedrawCharts();
        UpdatePlaybackButtonState();
    }

    private async Task InitializeMapAsync()
    {
        try
        {
            string templatePath = _viewModel.GetMapTemplatePath();
            if (!File.Exists(templatePath))
            {
                _viewModel.StatusText = $"Шаблон карты не найден: {templatePath}";
                return;
            }

            await MapWebView.EnsureCoreWebView2Async();
            MapWebView.Source = new Uri(templatePath);
            _viewModel.StatusText = "Карта инициализируется...";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Не удалось инициализировать встроенную карту.\n{ex.Message}\n\nПроверьте, что установлен WebView2 Runtime.",
                "Ошибка WebView2",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void MapWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _isMapReady = e.IsSuccess;

        if (_isMapReady)
            await SyncMapAsync();
    }

    private async void OpenCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Выберите CSV-файл телеметрии"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            StopPlayback();
            await _viewModel.LoadCsvAsync(dialog.FileName);
            RedrawCharts();
            await SyncMapAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка загрузки CSV", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RefreshMap_Click(object sender, RoutedEventArgs e)
    {
        await SyncMapAsync();
    }

    private void OpenMap_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _viewModel.OpenMapInBrowser();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка открытия карты", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Clear_Click(object sender, RoutedEventArgs e)
    {
        StopPlayback();
        _viewModel.Clear();
        SpeedChartCanvas.Children.Clear();
        LeanChartCanvas.Children.Clear();
        AccelChartCanvas.Children.Clear();
        await ClearMapAsync();
    }

    private async void ResetFilter_Click(object sender, RoutedEventArgs e)
    {
        StopPlayback();
        _viewModel.ResetFilter();
        RedrawCharts();
        await SyncMapAsync();
    }

    private void PrevPoint_Click(object sender, RoutedEventArgs e)
    {
        StopPlayback();
        _viewModel.MoveSelection(-1);
    }

    private void NextPoint_Click(object sender, RoutedEventArgs e)
    {
        StopPlayback();
        _viewModel.MoveSelection(1);
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.HasPoints)
            return;

        if (_playbackTimer.IsEnabled)
            StopPlayback();
        else
            StartPlayback();
    }

    private void PlaybackTimer_Tick(object? sender, EventArgs e)
    {
        if (!_viewModel.HasPoints)
        {
            StopPlayback();
            return;
        }

        bool moved = _viewModel.MoveSelection(1);
        if (!moved || _viewModel.PlaybackPosition >= _viewModel.PlaybackMaximum)
            StopPlayback();
    }

    private void StartPlayback()
    {
        if (!_viewModel.HasPoints)
            return;

        UpdatePlaybackTimerInterval();

        if (_viewModel.PlaybackPosition >= _viewModel.PlaybackMaximum)
            _viewModel.SelectPointByIndex(1);

        _playbackTimer.Start();
        UpdatePlaybackButtonState();
        _viewModel.StatusText = $"Воспроизведение маршрута запущено ({_viewModel.SelectedPlaybackSpeed.Label}).";
    }

    private void StopPlayback()
    {
        if (_playbackTimer.IsEnabled)
        {
            _playbackTimer.Stop();
            _viewModel.StatusText = "Воспроизведение остановлено.";
        }

        UpdatePlaybackButtonState();
    }

    private void UpdatePlaybackTimerInterval()
    {
        _playbackTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(50, _viewModel.PlaybackIntervalMilliseconds));
    }

    private void UpdatePlaybackButtonState()
    {
        if (PlayPauseButton is null)
            return;

        PlayPauseButton.Content = _playbackTimer.IsEnabled ? "❚❚ Пауза" : "▶ Пуск";
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedPoint))
        {
            RedrawCharts();
            _ = SyncSelectedPointAsync();
        }
        else if (e.PropertyName == nameof(MainViewModel.Statistics)
            || e.PropertyName == nameof(MainViewModel.PlaybackPosition))
        {
            RedrawCharts();
        }
        else if (e.PropertyName == nameof(MainViewModel.HasPoints))
        {
            UpdatePlaybackButtonState();
        }
        else if (e.PropertyName == nameof(MainViewModel.FilterSummary))
        {
            StopPlayback();
            RedrawCharts();
            _ = SyncMapAsync();
        }
        else if (e.PropertyName == nameof(MainViewModel.SelectedPlaybackSpeed))
        {
            UpdatePlaybackTimerInterval();
            if (_playbackTimer.IsEnabled)
                _viewModel.StatusText = $"Скорость воспроизведения изменена: {_viewModel.SelectedPlaybackSpeed.Label}.";
        }
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawCharts();
    }

    private void RedrawCharts()
    {
        int? selectedIndex = _viewModel.SelectedPoint is null ? null : _viewModel.PlaybackPosition;

        DrawSeries(
            SpeedChartCanvas,
            _viewModel.SpeedSeries,
            "км/ч",
            selectedIndex,
            "#38BDF8",
            "Скорость");

        DrawSeries(
            LeanChartCanvas,
            _viewModel.LeanSeries,
            "°",
            selectedIndex,
            "#A78BFA",
            "Наклон");

        DrawMultiSeries(
            AccelChartCanvas,
            new[]
            {
                new ChartSeries("Accel X", _viewModel.AccelXSeries, "#22C55E"),
                new ChartSeries("Accel Y", _viewModel.AccelYSeries, "#F59E0B"),
                new ChartSeries("Accel Z", _viewModel.AccelZSeries, "#EF4444")
            },
            "ед.",
            selectedIndex);
    }

    private async Task SyncMapAsync()
    {
        if (!_isMapReady)
            return;

        if (_viewModel.Points.Count == 0)
        {
            await ClearMapAsync();
            return;
        }

        string json = _viewModel.GetRouteJson();
        await MapWebView.ExecuteScriptAsync($"window.setRouteData({json});");
        await SyncSelectedPointAsync();
        _viewModel.StatusText = $"Карта обновлена. Точек в текущем диапазоне: {_viewModel.Points.Count}.";
    }

    private async Task SyncSelectedPointAsync()
    {
        if (!_isMapReady)
            return;

        if (_viewModel.SelectedPoint is null)
        {
            await MapWebView.ExecuteScriptAsync("window.setSelectedIndex(null);");
            return;
        }

        await MapWebView.ExecuteScriptAsync($"window.setSelectedIndex({_viewModel.SelectedPoint.Index});");
    }

    private async Task ClearMapAsync()
    {
        if (!_isMapReady)
            return;

        await MapWebView.ExecuteScriptAsync("window.clearRouteData();");
    }

    private static void DrawSeries(Canvas canvas, IReadOnlyList<double> values, string unit, int? selectedIndex, string colorHex, string label)
    {
        canvas.Children.Clear();

        if (values.Count == 0 || canvas.ActualWidth < 10 || canvas.ActualHeight < 10)
            return;

        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;
        var palette = CreatePalette(colorHex);

        (double min, double max) = GetRange(values);
        DrawAxes(canvas, width, height, min, max, unit, values.Count, palette);
        var polyline = CreatePolyline(values, width, height, min, max, palette.LineBrush);
        canvas.Children.Add(polyline);

        DrawSelectedPoint(canvas, values, width, height, min, max, selectedIndex, unit, palette.SelectedBrush, label);
    }

    private static void DrawMultiSeries(Canvas canvas, IReadOnlyList<ChartSeries> seriesSet, string unit, int? selectedIndex)
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

        if (selectedIndex.HasValue)
        {
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
            polyline.Points.Add(new Point(x, y));
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

    private sealed record ChartSeries(string Label, IReadOnlyList<double> Values, string ColorHex);

    private sealed record ChartPalette(
        Brush GridBrush,
        Brush AxisBrush,
        Brush LineBrush,
        Brush TextBrush,
        Brush SelectedBrush);
}
