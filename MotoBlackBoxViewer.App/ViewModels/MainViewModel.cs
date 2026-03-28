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
    private readonly List<TelemetryPoint> _allPoints = new();

    private string _statusText = "Готово. Откройте CSV-файл.";
    private TelemetryPoint? _selectedPoint;
    private TelemetryStatistics _statistics = new();
    private string? _currentFilePath;
    private int _playbackPosition;
    private int _filterStartIndex;
    private int _filterEndIndex;
    private PlaybackSpeedOption _selectedPlaybackSpeed;

    public MainViewModel()
    {
        PlaybackSpeedOptions = new[]
        {
            new PlaybackSpeedOption("0.25x", 0.25),
            new PlaybackSpeedOption("0.5x", 0.5),
            new PlaybackSpeedOption("1x", 1.0),
            new PlaybackSpeedOption("2x", 2.0),
            new PlaybackSpeedOption("4x", 4.0)
        };

        _selectedPlaybackSpeed = PlaybackSpeedOptions[2];
    }

    public ObservableCollection<TelemetryPoint> Points { get; } = new();

    public IReadOnlyList<PlaybackSpeedOption> PlaybackSpeedOptions { get; }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string CurrentFileName => string.IsNullOrWhiteSpace(_currentFilePath)
        ? "Файл не загружен"
        : Path.GetFileName(_currentFilePath);

    public bool HasPoints => Points.Count > 0;

    public bool HasSourceData => _allPoints.Count > 0;

    public TelemetryPoint? SelectedPoint
    {
        get => _selectedPoint;
        set => SetSelectedPoint(value, syncPlayback: true);
    }

    public string SelectedPointSummary
    {
        get
        {
            if (SelectedPoint is null)
                return "Точка не выбрана";

            int visiblePosition = GetVisiblePositionOf(SelectedPoint);
            return $"вид. {visiblePosition}/{Points.Count} · исх. #{SelectedPoint.Index} · {SelectedPoint.Latitude:F6}, {SelectedPoint.Longitude:F6} · {SelectedPoint.SpeedKmh:F1} км/ч · наклон {SelectedPoint.LeanAngleDeg:F1}° · AX {SelectedPoint.AccelX:F2} · AY {SelectedPoint.AccelY:F2} · AZ {SelectedPoint.AccelZ:F2}";
        }
    }

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

    public string PlaybackSummary
    {
        get
        {
            if (!HasPoints)
                return "Нет данных";

            string sourceSuffix = SelectedPoint is null ? string.Empty : $" · исх. #{SelectedPoint.Index}";
            return $"Точка {PlaybackPosition} / {Points.Count}{sourceSuffix}";
        }
    }

    public int FilterMinimum => HasSourceData ? 1 : 0;
    public int FilterMaximum => _allPoints.Count;

    public int FilterStartIndex
    {
        get => _filterStartIndex;
        set
        {
            int normalized = NormalizeFilterValue(value, fallbackToMaximum: false);
            if (HasSourceData && normalized > _filterEndIndex)
            {
                _filterEndIndex = normalized;
                RaisePropertyChanged(nameof(FilterEndIndex));
            }

            if (SetProperty(ref _filterStartIndex, normalized))
                ApplyFilter(preserveSelection: true, updateStatus: true);
        }
    }

    public int FilterEndIndex
    {
        get => _filterEndIndex;
        set
        {
            int normalized = NormalizeFilterValue(value, fallbackToMaximum: true);
            if (HasSourceData && normalized < _filterStartIndex)
            {
                _filterStartIndex = normalized;
                RaisePropertyChanged(nameof(FilterStartIndex));
            }

            if (SetProperty(ref _filterEndIndex, normalized))
                ApplyFilter(preserveSelection: true, updateStatus: true);
        }
    }

    public string FilterSummary => !HasSourceData
        ? "Фильтр недоступен"
        : $"Диапазон #{FilterStartIndex}–#{FilterEndIndex} · показано {Points.Count} из {_allPoints.Count} точек";

    public PlaybackSpeedOption SelectedPlaybackSpeed
    {
        get => _selectedPlaybackSpeed;
        set
        {
            if (value is null)
                return;

            if (SetProperty(ref _selectedPlaybackSpeed, value))
            {
                RaisePropertyChanged(nameof(PlaybackSpeedSummary));
                RaisePropertyChanged(nameof(PlaybackIntervalMilliseconds));
            }
        }
    }

    public string PlaybackSpeedSummary => $"Скорость: {SelectedPlaybackSpeed.Label}";

    public int PlaybackIntervalMilliseconds => (int)Math.Round(350d / SelectedPlaybackSpeed.Multiplier);

    public async Task LoadCsvAsync(string filePath, CancellationToken cancellationToken = default)
    {
        StatusText = "Читаю CSV...";

        var points = await _reader.ReadAsync(filePath, cancellationToken);

        _allPoints.Clear();
        _allPoints.AddRange(points);
        _currentFilePath = filePath;

        _filterStartIndex = _allPoints.Count == 0 ? 0 : 1;
        _filterEndIndex = _allPoints.Count;

        RaisePropertyChanged(nameof(CurrentFileName));
        RaisePropertyChanged(nameof(FilterMinimum));
        RaisePropertyChanged(nameof(FilterMaximum));
        RaisePropertyChanged(nameof(FilterStartIndex));
        RaisePropertyChanged(nameof(FilterEndIndex));

        ApplyFilter(preserveSelection: false, updateStatus: false);
        StatusText = $"Загружено точек: {_allPoints.Count}. Показано: {Points.Count}. Файл: {Path.GetFileName(filePath)}";
    }

    public void Clear()
    {
        _allPoints.Clear();
        Points.Clear();
        _currentFilePath = null;
        _filterStartIndex = 0;
        _filterEndIndex = 0;
        Statistics = new TelemetryStatistics();

        RaiseCollectionDependentProperties();
        RaisePropertyChanged(nameof(CurrentFileName));
        RaisePropertyChanged(nameof(FilterMinimum));
        RaisePropertyChanged(nameof(FilterMaximum));
        RaisePropertyChanged(nameof(FilterStartIndex));
        RaisePropertyChanged(nameof(FilterEndIndex));
        RaisePropertyChanged(nameof(FilterSummary));

        SetSelectedPoint(null, syncPlayback: true);
        StatusText = "Данные очищены.";
    }

    public void ResetFilter()
    {
        if (!HasSourceData)
            return;

        _filterStartIndex = 1;
        _filterEndIndex = _allPoints.Count;
        RaisePropertyChanged(nameof(FilterStartIndex));
        RaisePropertyChanged(nameof(FilterEndIndex));
        ApplyFilter(preserveSelection: true, updateStatus: true);
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

    private void ApplyFilter(bool preserveSelection, bool updateStatus)
    {
        TelemetryPoint? preferredPoint = preserveSelection ? SelectedPoint : null;

        Points.Clear();

        if (!HasSourceData)
        {
            Statistics = new TelemetryStatistics();
            RaiseCollectionDependentProperties();
            SetSelectedPoint(null, syncPlayback: true);
            return;
        }

        int start = NormalizeFilterValue(_filterStartIndex, fallbackToMaximum: false);
        int end = NormalizeFilterValue(_filterEndIndex, fallbackToMaximum: true);

        if (start != _filterStartIndex)
        {
            _filterStartIndex = start;
            RaisePropertyChanged(nameof(FilterStartIndex));
        }

        if (end != _filterEndIndex)
        {
            _filterEndIndex = end;
            RaisePropertyChanged(nameof(FilterEndIndex));
        }

        var filtered = _allPoints
            .Where(p => p.Index >= start && p.Index <= end)
            .ToList();

        foreach (var point in filtered)
            Points.Add(point);

        Statistics = _analyzer.Analyze(filtered);
        RaiseCollectionDependentProperties();

        TelemetryPoint? nextPoint = null;
        if (preferredPoint is not null)
            nextPoint = Points.FirstOrDefault(p => p.Index == preferredPoint.Index);

        nextPoint ??= Points.FirstOrDefault();
        SetSelectedPoint(nextPoint, syncPlayback: true);
        RaisePropertyChanged(nameof(FilterSummary));

        if (updateStatus)
            StatusText = $"Применен диапазон #{FilterStartIndex}–#{FilterEndIndex}. Показано {Points.Count} точек.";
    }

    private void RaiseCollectionDependentProperties()
    {
        RaisePropertyChanged(nameof(HasPoints));
        RaisePropertyChanged(nameof(HasSourceData));
        RaisePropertyChanged(nameof(SpeedSeries));
        RaisePropertyChanged(nameof(LeanSeries));
        RaisePropertyChanged(nameof(AccelXSeries));
        RaisePropertyChanged(nameof(AccelYSeries));
        RaisePropertyChanged(nameof(AccelZSeries));
        RaisePropertyChanged(nameof(PlaybackMinimum));
        RaisePropertyChanged(nameof(PlaybackMaximum));
        RaisePropertyChanged(nameof(PlaybackSummary));
        RaisePropertyChanged(nameof(SelectedPointSummary));
    }

    private int NormalizeFilterValue(int value, bool fallbackToMaximum)
    {
        if (!HasSourceData)
            return 0;

        int fallback = fallbackToMaximum ? _allPoints.Count : 1;
        if (value <= 0)
            return fallback;

        return Math.Clamp(value, 1, _allPoints.Count);
    }

    private void SetSelectedPoint(TelemetryPoint? value, bool syncPlayback)
    {
        bool changed = SetProperty(ref _selectedPoint, value, nameof(SelectedPoint));

        if (!changed)
            return;

        RaisePropertyChanged(nameof(SelectedPointSummary));
        RaisePropertyChanged(nameof(PlaybackSummary));

        if (syncPlayback)
        {
            int targetPosition = GetVisiblePositionOf(value);
            SetPlaybackPosition(targetPosition, syncSelectedPoint: false);
        }
    }

    private int GetVisiblePositionOf(TelemetryPoint? point)
    {
        if (point is null)
            return 0;

        int zeroBased = Points.IndexOf(point);
        if (zeroBased < 0)
            zeroBased = Points.ToList().FindIndex(p => p.Index == point.Index);

        return zeroBased >= 0 ? zeroBased + 1 : 0;
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

public sealed record PlaybackSpeedOption(string Label, double Multiplier);
