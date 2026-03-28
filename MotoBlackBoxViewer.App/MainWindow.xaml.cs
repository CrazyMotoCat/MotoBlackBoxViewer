using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using MotoBlackBoxViewer.App.ViewModels;

namespace MotoBlackBoxViewer.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private bool _isMapReady;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        Loaded += MainWindow_Loaded;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeMapAsync();
        RedrawCharts();
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

    private async void MapWebView_NavigationCompleted(object? sender, object e)
    {
        _isMapReady = true;
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
        _viewModel.Clear();
        SpeedChartCanvas.Children.Clear();
        LeanChartCanvas.Children.Clear();
        await ClearMapAsync();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedPoint))
        {
            RedrawCharts();
            _ = SyncSelectedPointAsync();
        }
        else if (e.PropertyName == nameof(MainViewModel.Statistics))
        {
            RedrawCharts();
        }
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawCharts();
    }

    private void RedrawCharts()
    {
        int? selectedIndex = _viewModel.SelectedPoint?.Index;
        DrawSeries(SpeedChartCanvas, _viewModel.SpeedSeries, "км/ч", selectedIndex);
        DrawSeries(LeanChartCanvas, _viewModel.LeanSeries, "°", selectedIndex);
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
        _viewModel.StatusText = $"Карта обновлена. Точек на маршруте: {_viewModel.Points.Count}.";
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

    private static void DrawSeries(System.Windows.Controls.Canvas canvas, IReadOnlyList<double> values, string unit, int? selectedIndex)
    {
        canvas.Children.Clear();

        if (values.Count == 0 || canvas.ActualWidth < 10 || canvas.ActualHeight < 10)
            return;

        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;
        double marginLeft = 42;
        double marginRight = 12;
        double marginTop = 12;
        double marginBottom = 28;

        double plotWidth = Math.Max(1, width - marginLeft - marginRight);
        double plotHeight = Math.Max(1, height - marginTop - marginBottom);

        double min = values.Min();
        double max = values.Max();
        if (Math.Abs(max - min) < 0.0001)
            max = min + 1;

        var gridBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
        var axisBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
        var lineBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
        var textBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB"));
        var selectedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F97316"));

        for (int i = 0; i < 5; i++)
        {
            double y = marginTop + (plotHeight / 4d) * i;
            canvas.Children.Add(new Line
            {
                X1 = marginLeft,
                X2 = marginLeft + plotWidth,
                Y1 = y,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1
            });
        }

        canvas.Children.Add(new Line
        {
            X1 = marginLeft,
            X2 = marginLeft,
            Y1 = marginTop,
            Y2 = marginTop + plotHeight,
            Stroke = axisBrush,
            StrokeThickness = 1.2
        });

        canvas.Children.Add(new Line
        {
            X1 = marginLeft,
            X2 = marginLeft + plotWidth,
            Y1 = marginTop + plotHeight,
            Y2 = marginTop + plotHeight,
            Stroke = axisBrush,
            StrokeThickness = 1.2
        });

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

        canvas.Children.Add(polyline);

        if (selectedIndex.HasValue)
        {
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

            canvas.Children.Add(new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = selectedBrush,
                Stroke = Brushes.White,
                StrokeThickness = 1.5,
                Margin = new Thickness(x - 5, y - 5, 0, 0)
            });

            AddLabel(canvas,
                $"выбрано: #{selectedIndex.Value} = {values[zeroBased].ToString("F1", CultureInfo.InvariantCulture)} {unit}",
                marginLeft,
                height - 24,
                selectedBrush);
        }

        AddLabel(canvas, $"max: {max.ToString("F1", CultureInfo.InvariantCulture)} {unit}", marginLeft, 0, textBrush);
        AddLabel(canvas, $"min: {min.ToString("F1", CultureInfo.InvariantCulture)} {unit}", marginLeft + 180, 0, textBrush);
        AddLabel(canvas, $"точек: {values.Count}", width - 110, 0, textBrush);
        AddLabel(canvas, min.ToString("F1", CultureInfo.InvariantCulture), 4, marginTop + plotHeight - 12, textBrush);
        AddLabel(canvas, max.ToString("F1", CultureInfo.InvariantCulture), 4, marginTop - 8, textBrush);
    }

    private static void AddLabel(System.Windows.Controls.Canvas canvas, string text, double x, double y, Brush brush)
    {
        var tb = new System.Windows.Controls.TextBlock
        {
            Text = text,
            Foreground = brush,
            FontSize = 12
        };

        System.Windows.Controls.Canvas.SetLeft(tb, x);
        System.Windows.Controls.Canvas.SetTop(tb, y);
        canvas.Children.Add(tb);
    }
}
