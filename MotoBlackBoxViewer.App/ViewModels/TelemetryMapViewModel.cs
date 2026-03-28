using System.ComponentModel;
using System.IO;
using MotoBlackBoxViewer.App.Helpers;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.App.ViewModels;

public sealed class TelemetryMapViewModel : ObservableObject, IDisposable
{
    private readonly TelemetryDataViewModel _data;
    private readonly TelemetrySelectionViewModel _selection;
    private readonly IMapExportService _mapExportService;
    private readonly TelemetrySessionState _state;
    private string _routeJson = "[]";
    private bool _isDisposed;

    public TelemetryMapViewModel(
        TelemetryDataViewModel data,
        TelemetrySelectionViewModel selection,
        IMapExportService mapExportService,
        TelemetrySessionState state)
    {
        _data = data;
        _selection = selection;
        _mapExportService = mapExportService;
        _state = state;

        _data.PropertyChanged += Data_PropertyChanged;
        _selection.PropertyChanged += Selection_PropertyChanged;
    }

    public string RouteJson => _routeJson;

    public int? SelectedPointIndex => _selection.SelectedPointIndex;

    public int RefreshVersion
    {
        get => _state.MapRefreshVersion;
        private set
        {
            if (_state.MapRefreshVersion == value)
                return;

            _state.MapRefreshVersion = value;
            RaisePropertyChanged();
        }
    }

    public void RequestRefresh() => RefreshVersion++;

    public string ExportMapHtml()
    {
        if (_data.Points.Count == 0)
            throw new InvalidOperationException("Сначала загрузите CSV-файл.");

        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MotoBlackBoxViewer",
            "Maps");

        return _mapExportService.ExportHtml(_data.Points, baseDir);
    }

    public void OpenInBrowser(string htmlPath) => _mapExportService.OpenInBrowser(htmlPath);

    private void Data_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TelemetryDataViewModel.VisibleDataVersion))
            UpdateRouteJson();
    }

    private void Selection_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TelemetrySelectionViewModel.SelectedPointIndex))
            RaisePropertyChanged(nameof(SelectedPointIndex));
    }

    private void UpdateRouteJson()
    {
        string nextRouteJson = _mapExportService.BuildRouteJson(_data.Points);
        if (string.Equals(_routeJson, nextRouteJson, StringComparison.Ordinal))
            return;

        _routeJson = nextRouteJson;
        RaisePropertyChanged(nameof(RouteJson));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _data.PropertyChanged -= Data_PropertyChanged;
        _selection.PropertyChanged -= Selection_PropertyChanged;
    }
}
