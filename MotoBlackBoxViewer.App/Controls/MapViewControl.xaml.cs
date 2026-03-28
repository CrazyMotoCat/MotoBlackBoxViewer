using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace MotoBlackBoxViewer.App.Controls;

public partial class MapViewControl : UserControl
{
    private bool _isMapReady;
    private string _appliedRouteJson = string.Empty;
    private int _appliedRefreshVersion = -1;
    private int? _appliedSelectedPointIndex;

    public static readonly DependencyProperty RouteJsonProperty = DependencyProperty.Register(
        nameof(RouteJson),
        typeof(string),
        typeof(MapViewControl),
        new PropertyMetadata(string.Empty, OnRouteStateChanged));

    public static readonly DependencyProperty SelectedPointIndexProperty = DependencyProperty.Register(
        nameof(SelectedPointIndex),
        typeof(int?),
        typeof(MapViewControl),
        new PropertyMetadata(null, OnSelectedPointIndexChanged));

    public static readonly DependencyProperty RefreshVersionProperty = DependencyProperty.Register(
        nameof(RefreshVersion),
        typeof(int),
        typeof(MapViewControl),
        new PropertyMetadata(0, OnRouteStateChanged));

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

    private static void OnRouteStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        _ = ((MapViewControl)d).SyncRouteStateAsync();
    }

    private static void OnSelectedPointIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        _ = ((MapViewControl)d).SyncSelectedPointAsync();
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
        {
            _appliedRouteJson = string.Empty;
            _appliedRefreshVersion = -1;
            _appliedSelectedPointIndex = null;
            await SyncRouteStateAsync(force: true);
            await SyncSelectedPointAsync(force: true);
        }
    }

    private async Task SyncRouteStateAsync(bool force = false)
    {
        if (!_isMapReady || MapWebView.CoreWebView2 is null)
            return;

        bool routeChanged = !string.Equals(_appliedRouteJson, RouteJson, StringComparison.Ordinal);
        bool refreshRequested = _appliedRefreshVersion != RefreshVersion;
        if (!force && !routeChanged && !refreshRequested)
            return;

        if (string.IsNullOrWhiteSpace(RouteJson) || RouteJson == "[]")
        {
            await MapWebView.ExecuteScriptAsync("window.clearRouteData();");
            _appliedRouteJson = RouteJson;
            _appliedRefreshVersion = RefreshVersion;
            _appliedSelectedPointIndex = null;
            return;
        }

        await MapWebView.ExecuteScriptAsync($"window.setRouteData({RouteJson});");
        _appliedRouteJson = RouteJson;
        _appliedRefreshVersion = RefreshVersion;
        _appliedSelectedPointIndex = null;
        await SyncSelectedPointAsync(force: true);
    }

    private async Task SyncSelectedPointAsync(bool force = false)
    {
        if (!_isMapReady || MapWebView.CoreWebView2 is null)
            return;

        if (!force && _appliedSelectedPointIndex == SelectedPointIndex)
            return;

        if (SelectedPointIndex.HasValue)
            await MapWebView.ExecuteScriptAsync($"window.setSelectedIndex({SelectedPointIndex.Value});");
        else
            await MapWebView.ExecuteScriptAsync("window.setSelectedIndex(null);");

        _appliedSelectedPointIndex = SelectedPointIndex;
    }
}
