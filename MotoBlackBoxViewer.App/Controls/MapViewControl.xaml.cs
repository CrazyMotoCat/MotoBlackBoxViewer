using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace MotoBlackBoxViewer.App.Controls;

public partial class MapViewControl : UserControl
{
    private bool _isMapReady;

    public static readonly DependencyProperty RouteJsonProperty = DependencyProperty.Register(
        nameof(RouteJson),
        typeof(string),
        typeof(MapViewControl),
        new PropertyMetadata(string.Empty, OnMapPropertyChanged));

    public static readonly DependencyProperty SelectedPointIndexProperty = DependencyProperty.Register(
        nameof(SelectedPointIndex),
        typeof(int?),
        typeof(MapViewControl),
        new PropertyMetadata(null, OnMapPropertyChanged));

    public static readonly DependencyProperty RefreshVersionProperty = DependencyProperty.Register(
        nameof(RefreshVersion),
        typeof(int),
        typeof(MapViewControl),
        new PropertyMetadata(0, OnMapPropertyChanged));

    public MapViewControl()
    {
        InitializeComponent();
        Loaded += MapViewControl_Loaded;
    }

    public string RouteJson
    {
        get => (string)GetValue(RouteJsonProperty);
        set => SetValue(RouteJsonProperty, value);
    }

    public int? SelectedPointIndex
    {
        get => (int?)GetValue(SelectedPointIndexProperty);
        set => SetValue(SelectedPointIndexProperty, value);
    }

    public int RefreshVersion
    {
        get => (int)GetValue(RefreshVersionProperty);
        set => SetValue(RefreshVersionProperty, value);
    }

    private async void MapViewControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isMapReady)
            return;

        await InitializeMapAsync();
    }

    private static void OnMapPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        _ = ((MapViewControl)d).SyncMapStateAsync();
    }

    private async Task InitializeMapAsync()
    {
        try
        {
            string templatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Map", "index.html");
            if (!File.Exists(templatePath))
                return;

            await MapWebView.EnsureCoreWebView2Async();
            MapWebView.Source = new Uri(templatePath);
        }
        catch
        {
            // Ошибку не пробрасываем: control должен деградировать мягко.
        }
    }

    private async void MapWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _isMapReady = e.IsSuccess;
        if (_isMapReady)
            await SyncMapStateAsync();
    }

    private async Task SyncMapStateAsync()
    {
        if (!_isMapReady || MapWebView.CoreWebView2 is null)
            return;

        if (string.IsNullOrWhiteSpace(RouteJson) || RouteJson == "[]")
        {
            await MapWebView.ExecuteScriptAsync("window.clearRouteData();");
            return;
        }

        await MapWebView.ExecuteScriptAsync($"window.setRouteData({RouteJson});");

        if (SelectedPointIndex.HasValue)
            await MapWebView.ExecuteScriptAsync($"window.setSelectedIndex({SelectedPointIndex.Value});");
        else
            await MapWebView.ExecuteScriptAsync("window.setSelectedIndex(null);");
    }
}
