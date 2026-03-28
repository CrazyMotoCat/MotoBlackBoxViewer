using System.Collections.ObjectModel;
using MotoBlackBoxViewer.App.Helpers;
using MotoBlackBoxViewer.App.Services;
using MotoBlackBoxViewer.Core.Models;
using MotoBlackBoxViewer.Core.Services;

namespace MotoBlackBoxViewer.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly CsvTelemetryReader _reader = new();
    private readonly TelemetryAnalyzer _analyzer = new();
    private readonly MapExportService _mapExportService = new();

    private string _statusText = "Готово. Откройте CSV-файл.";
    private TelemetryPoint? _selectedPoint;
    private TelemetryStatistics _statistics = new();
    private string? _currentFilePath;

    public ObservableCollection<TelemetryPoint> Points { get; } = new();

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string CurrentFileName => string.IsNullOrWhiteSpace(_currentFilePath)
        ? "Файл не загружен"
        : Path.GetFileName(_currentFilePath);

    public TelemetryPoint? SelectedPoint
    {
        get => _selectedPoint;
        set
        {
            if (SetProperty(ref _selectedPoint, value))
                RaisePropertyChanged(nameof(SelectedPointSummary));
        }
    }

    public string SelectedPointSummary => SelectedPoint is null
        ? "Точка не выбрана"
        : $"#{SelectedPoint.Index} · {SelectedPoint.Latitude:F6}, {SelectedPoint.Longitude:F6} · {SelectedPoint.SpeedKmh:F1} км/ч · наклон {SelectedPoint.LeanAngleDeg:F1}° · дистанция {SelectedPoint.DistanceFromStartMeters:F1} м";

    public TelemetryStatistics Statistics
    {
        get => _statistics;
        private set => SetProperty(ref _statistics, value);
    }

    public IReadOnlyList<double> SpeedSeries => Points.Select(p => p.SpeedKmh).ToArray();
    public IReadOnlyList<double> LeanSeries => Points.Select(p => p.LeanAngleDeg).ToArray();

    public async Task LoadCsvAsync(string filePath, CancellationToken cancellationToken = default)
    {
        StatusText = "Читаю CSV...";

        var points = await _reader.ReadAsync(filePath, cancellationToken);

        Points.Clear();
        foreach (var point in points)
            Points.Add(point);

        _currentFilePath = filePath;
        Statistics = _analyzer.Analyze(points);
        SelectedPoint = Points.FirstOrDefault();

        RaisePropertyChanged(nameof(SpeedSeries));
        RaisePropertyChanged(nameof(LeanSeries));
        RaisePropertyChanged(nameof(CurrentFileName));
        RaisePropertyChanged(nameof(SelectedPointSummary));

        StatusText = $"Загружено точек: {Points.Count}. Файл: {Path.GetFileName(filePath)}";
    }

    public void Clear()
    {
        Points.Clear();
        SelectedPoint = null;
        Statistics = new TelemetryStatistics();
        _currentFilePath = null;
        RaisePropertyChanged(nameof(SpeedSeries));
        RaisePropertyChanged(nameof(LeanSeries));
        RaisePropertyChanged(nameof(CurrentFileName));
        RaisePropertyChanged(nameof(SelectedPointSummary));
        StatusText = "Данные очищены.";
    }

    public string GetRouteJson() => _mapExportService.BuildRouteJson(Points);

    public string GetMapTemplatePath() => _mapExportService.GetTemplatePath();

    public string ExportMapHtml()
    {
        if (Points.Count == 0)
            throw new InvalidOperationException("Сначала загрузите CSV-файл.");

        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MotoBlackBoxViewer",
            "Maps");

        string htmlPath = _mapExportService.ExportHtml(Points, baseDir);
        StatusText = $"Карта подготовлена: {htmlPath}";
        return htmlPath;
    }

    public void OpenMapInBrowser()
    {
        string htmlPath = ExportMapHtml();
        _mapExportService.OpenInBrowser(htmlPath);
    }
}
