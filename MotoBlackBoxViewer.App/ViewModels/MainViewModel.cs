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
    private int _playbackPosition;

    public ObservableCollection<TelemetryPoint> Points { get; } = new();

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string CurrentFileName => string.IsNullOrWhiteSpace(_currentFilePath)
        ? "Файл не загружен"
        : Path.GetFileName(_currentFilePath);

    public bool HasPoints => Points.Count > 0;

    public TelemetryPoint? SelectedPoint
    {
        get => _selectedPoint;
        set => SetSelectedPoint(value, syncPlayback: true);
    }

    public string SelectedPointSummary => SelectedPoint is null
        ? "Точка не выбрана"
        : $"#{SelectedPoint.Index} · {SelectedPoint.Latitude:F6}, {SelectedPoint.Longitude:F6} · {SelectedPoint.SpeedKmh:F1} км/ч · наклон {SelectedPoint.LeanAngleDeg:F1}° · AX {SelectedPoint.AccelX:F2} · AY {SelectedPoint.AccelY:F2} · AZ {SelectedPoint.AccelZ:F2}";

    public TelemetryStatistics Statistics
    {
        get => _statistics;
        private set => SetProperty(ref _statistics, value);
    }

    public IReadOnlyList<double> SpeedSeries => Points.Select(p => p.SpeedKmh).ToArray();
    public IReadOnlyList<double> LeanSeries => Points.Select(p => p.LeanAngleDeg).ToArray();
    public IReadOnlyList<double> AccelXSeries => Points.Select(p => p.AccelX).ToArray();
    public IReadOnlyList<double> AccelYSeries => Points.Select(p => p.AccelY).ToArray();
    public IReadOnlyList<double> AccelZSeries => Points.Select(p => p.AccelZ).ToArray();

    public int PlaybackMinimum => HasPoints ? 1 : 0;
    public int PlaybackMaximum => Points.Count;

    public int PlaybackPosition
    {
        get => _playbackPosition;
        set => SetPlaybackPosition(value, syncSelectedPoint: true);
    }

    public string PlaybackSummary => !HasPoints
        ? "Нет данных"
        : $"Точка {PlaybackPosition} / {Points.Count}";

    public async Task LoadCsvAsync(string filePath, CancellationToken cancellationToken = default)
    {
        StatusText = "Читаю CSV...";

        var points = await _reader.ReadAsync(filePath, cancellationToken);

        Points.Clear();
        foreach (var point in points)
            Points.Add(point);

        _currentFilePath = filePath;
        Statistics = _analyzer.Analyze(points);

        RaisePropertyChanged(nameof(HasPoints));
        RaisePropertyChanged(nameof(SpeedSeries));
        RaisePropertyChanged(nameof(LeanSeries));
        RaisePropertyChanged(nameof(AccelXSeries));
        RaisePropertyChanged(nameof(AccelYSeries));
        RaisePropertyChanged(nameof(AccelZSeries));
        RaisePropertyChanged(nameof(CurrentFileName));
        RaisePropertyChanged(nameof(PlaybackMinimum));
        RaisePropertyChanged(nameof(PlaybackMaximum));

        SetSelectedPoint(Points.FirstOrDefault(), syncPlayback: true);
        StatusText = $"Загружено точек: {Points.Count}. Файл: {Path.GetFileName(filePath)}";
    }

    public void Clear()
    {
        Points.Clear();
        _currentFilePath = null;
        Statistics = new TelemetryStatistics();

        RaisePropertyChanged(nameof(HasPoints));
        RaisePropertyChanged(nameof(SpeedSeries));
        RaisePropertyChanged(nameof(LeanSeries));
        RaisePropertyChanged(nameof(AccelXSeries));
        RaisePropertyChanged(nameof(AccelYSeries));
        RaisePropertyChanged(nameof(AccelZSeries));
        RaisePropertyChanged(nameof(CurrentFileName));
        RaisePropertyChanged(nameof(PlaybackMinimum));
        RaisePropertyChanged(nameof(PlaybackMaximum));

        SetSelectedPoint(null, syncPlayback: true);
        StatusText = "Данные очищены.";
    }

    public void SelectPointByIndex(int oneBasedIndex)
        => SetPlaybackPosition(oneBasedIndex, syncSelectedPoint: true);

    public bool MoveSelection(int delta)
    {
        if (!HasPoints)
            return false;

        int target = PlaybackPosition <= 0 ? 1 : PlaybackPosition + delta;
        int clamped = Math.Clamp(target, 1, PlaybackMaximum);
        bool changed = clamped != PlaybackPosition;
        SetPlaybackPosition(clamped, syncSelectedPoint: true);
        return changed;
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

    private void SetSelectedPoint(TelemetryPoint? value, bool syncPlayback)
    {
        bool changed = SetProperty(ref _selectedPoint, value, nameof(SelectedPoint));

        if (!changed)
            return;

        RaisePropertyChanged(nameof(SelectedPointSummary));

        if (syncPlayback)
        {
            int targetPosition = value?.Index ?? 0;
            SetPlaybackPosition(targetPosition, syncSelectedPoint: false);
        }
    }

    private void SetPlaybackPosition(int value, bool syncSelectedPoint)
    {
        int clamped = !HasPoints ? 0 : Math.Clamp(value, 1, PlaybackMaximum);
        bool changed = SetProperty(ref _playbackPosition, clamped, nameof(PlaybackPosition));

        if (!changed)
            return;

        RaisePropertyChanged(nameof(PlaybackSummary));

        if (syncSelectedPoint)
        {
            TelemetryPoint? point = clamped == 0 ? null : Points[clamped - 1];
            SetSelectedPoint(point, syncPlayback: false);
        }
    }
}
