using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using MotoBlackBoxViewer.App.Services;

namespace MotoBlackBoxViewer.App.Controls;

public partial class MapViewControl : UserControl
{
    private const string MapHostName = "appassets.motoblackboxviewer";
    private static readonly Uri MapPageUri = new($"https://{MapHostName}/index.html");

    private bool _isMapReady;
    private string _appliedRouteJson = string.Empty;
    private int _appliedRefreshVersion = -1;
    private int? _appliedSelectedPointIndex;
    private bool _routeSyncPending;
    private bool _selectionSyncPending;
    private bool _isSyncLoopRunning;

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
        ((MapViewControl)d).ScheduleSync(includeRoute: true, includeSelection: false);
    }

    private static void OnSelectedPointIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MapViewControl)d).ScheduleSync(includeRoute: false, includeSelection: true);
    }

    private async Task InitializeMapAsync()
    {
        try
        {
            string assetsFolder = Path.Combine(AppContext.BaseDirectory, "Assets", "Map");
            string templatePath = Path.Combine(assetsFolder, "index.html");
            if (!File.Exists(templatePath))
                return;

            await MapWebView.EnsureCoreWebView2Async();
            MapWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                MapHostName,
                assetsFolder,
                CoreWebView2HostResourceAccessKind.Allow);
            MapWebView.Source = MapPageUri;
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Failed to initialize map control: {ex}");
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
            await RunAndTraceAsync(() => SyncRouteStateAsync(force: true));
            await RunAndTraceAsync(() => SyncSelectedPointAsync(force: true));
        }
    }

    private void ScheduleSync(bool includeRoute, bool includeSelection)
    {
        if (includeRoute)
            _routeSyncPending = true;

        if (includeSelection)
            _selectionSyncPending = true;

        if (_isSyncLoopRunning)
            return;

        _isSyncLoopRunning = true;
        _ = RunAndTraceAsync(ProcessPendingSyncLoopAsync);
    }

    private static async Task RunAndTraceAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"MapViewControl async operation failed: {ex}");
        }
    }

    private async Task ProcessPendingSyncLoopAsync()
    {
        try
        {
            while (true)
            {
                bool syncRoute = _routeSyncPending;
                bool syncSelection = _selectionSyncPending;

                _routeSyncPending = false;
                _selectionSyncPending = false;

                if (!syncRoute && !syncSelection)
                    break;

                if (syncRoute)
                {
                    await SyncRouteStateAsync();
                    continue;
                }

                await SyncSelectedPointAsync();
            }
        }
        finally
        {
            _isSyncLoopRunning = false;

            if (_routeSyncPending || _selectionSyncPending)
                ScheduleSync(includeRoute: false, includeSelection: false);
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
            await MapWebView.ExecuteScriptAsync(MapScriptBuilder.BuildClearRouteDataScript());
            _appliedRouteJson = RouteJson;
            _appliedRefreshVersion = RefreshVersion;
            _appliedSelectedPointIndex = null;
            return;
        }

        await MapWebView.ExecuteScriptAsync(MapScriptBuilder.BuildSetRouteDataScript(RouteJson));
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

        await MapWebView.ExecuteScriptAsync(MapScriptBuilder.BuildSetSelectedIndexScript(SelectedPointIndex));
        _appliedSelectedPointIndex = SelectedPointIndex;
    }
}
