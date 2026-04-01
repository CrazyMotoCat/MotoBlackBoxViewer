using System.Collections.ObjectModel;
using MotoBlackBoxViewer.App.Helpers;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.App.Services;
using MotoBlackBoxViewer.Core.Interfaces;
using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.App.ViewModels;

public sealed class TelemetryDataViewModel : ObservableObject
{
    private static readonly ChartWindowOption[] DefaultChartWindowOptions =
    [
        new("50 до / 50 после", 50),
        new("100 до / 100 после", 100),
        new("200 до / 200 после", 200),
        new("500 до / 500 после", 500),
        new("1000 до / 1000 после", 1000),
        new("5000 до / 5000 после", 5000),
        new("Весь диапазон", 0)
    ];

    private readonly ICsvTelemetryReader _reader;
    private readonly TelemetryDataProcessor _dataProcessor;
    private readonly TelemetrySessionState _state;
    private IReadOnlyDictionary<int, int> _visiblePositionsByPointIndex = new Dictionary<int, int>();
    private TelemetrySeriesSnapshot _seriesSnapshot = TelemetrySeriesSnapshot.Empty;
    private int _visibleDataVersion;
    private ChartWindowOption _selectedChartWindow;

    public TelemetryDataViewModel(
        ICsvTelemetryReader reader,
        TelemetryDataProcessor dataProcessor,
        TelemetrySessionState state)
    {
        _reader = reader;
        _dataProcessor = dataProcessor;
        _state = state;
        _selectedChartWindow = ResolveChartWindowOption(_state.ChartWindowRadius);
        Points = new ReadOnlyObservableCollection<TelemetryPoint>(_state.VisiblePoints);
    }

    public ReadOnlyObservableCollection<TelemetryPoint> Points { get; }

    public IReadOnlyList<ChartWindowOption> ChartWindowOptions => DefaultChartWindowOptions;

    public ChartWindowOption SelectedChartWindow
    {
        get => _selectedChartWindow;
        set
        {
            if (value is null || Equals(_selectedChartWindow, value))
                return;

            _selectedChartWindow = value;
            _state.ChartWindowRadius = value.Radius;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ChartWindowRadius));
            RaisePropertyChanged(nameof(ChartWindowSummary));
        }
    }

    public int ChartWindowRadius => _selectedChartWindow.Radius;

    public string ChartWindowSummary => _selectedChartWindow.IsFullRange
        ? "График показывает весь текущий диапазон."
        : $"График показывает по {_selectedChartWindow.Radius} точек до и после текущей.";

    public string StatusText
    {
        get => _state.StatusText;
        set
        {
            if (_state.StatusText == value)
                return;

            _state.StatusText = value;
            RaisePropertyChanged();
        }
    }

    public string CurrentFileName => _state.CurrentFileName;

    public string ImportDiagnosticsText => _state.ImportDiagnosticsText;

    public bool HasImportDiagnostics => !string.IsNullOrWhiteSpace(_state.ImportDiagnosticsText);

    public bool HasPoints => _state.HasVisiblePoints;

    public bool HasSourceData => _state.HasSourceData;

    public TelemetryStatistics Statistics
    {
        get => _state.Statistics;
        private set
        {
            _state.Statistics = value;
            RaisePropertyChanged();
        }
    }

    public IReadOnlyList<double> SpeedSeries => _seriesSnapshot.SpeedSeries;

    public IReadOnlyList<double> LeanSeries => _seriesSnapshot.LeanSeries;

    public IReadOnlyList<double> AccelXSeries => _seriesSnapshot.AccelXSeries;

    public IReadOnlyList<double> AccelYSeries => _seriesSnapshot.AccelYSeries;

    public IReadOnlyList<double> AccelZSeries => _seriesSnapshot.AccelZSeries;

    public IReadOnlyList<ChartSeriesDefinition> AccelSeries => _seriesSnapshot.AccelSeries;

    public int VisibleDataVersion => _visibleDataVersion;

    public int FilterMinimum => HasSourceData ? 1 : 0;

    public int FilterMaximum => _state.AllPoints.Count;

    public int FilterStartIndex
    {
        get => _state.FilterStartIndex;
        set => SetFilterStartIndex(value);
    }

    public int FilterEndIndex
    {
        get => _state.FilterEndIndex;
        set => SetFilterEndIndex(value);
    }

    public string FilterSummary => !HasSourceData
        ? "Фильтр недоступен"
        : $"Диапазон #{FilterStartIndex}–#{FilterEndIndex} · показано {Points.Count} из {_state.AllPoints.Count} точек";

    public async Task LoadCsvAsync(string filePath, CancellationToken cancellationToken = default)
    {
        CsvTelemetryReadResult readResult = await _reader.ReadAsync(filePath, cancellationToken);
        IReadOnlyList<TelemetryPoint> points = readResult.Points;

        _state.AllPoints.Clear();
        _state.AllPoints.AddRange(points);
        _state.CurrentFilePath = filePath;
        _state.FilterStartIndex = _state.AllPoints.Count == 0 ? 0 : 1;
        _state.FilterEndIndex = _state.AllPoints.Count;
        SetImportDiagnosticsText(BuildImportDiagnosticsText(readResult));

        RaiseSourceDataProperties();
        RebuildVisibleData(preferredPoint: null, updateStatus: false);
    }

    public void Clear()
    {
        _state.AllPoints.Clear();
        _state.VisiblePoints.Clear();
        ApplyVisibleData(TelemetryVisibleData.Empty);
        _state.CurrentFilePath = null;
        _state.FilterStartIndex = 0;
        _state.FilterEndIndex = 0;
        SetImportDiagnosticsText(string.Empty);
        RaiseSourceDataProperties();
        RaiseVisibleDataProperties();
    }

    public void ResetFilterRange()
    {
        if (!HasSourceData)
            return;

        _state.FilterStartIndex = 1;
        _state.FilterEndIndex = _state.AllPoints.Count;
        RaisePropertyChanged(nameof(FilterStartIndex));
        RaisePropertyChanged(nameof(FilterEndIndex));
        RaisePropertyChanged(nameof(FilterSummary));
    }

    public void RestoreFilterRange(int start, int end)
    {
        if (!HasSourceData)
            return;

        _state.FilterStartIndex = NormalizeRestoredFilterValue(start, fallbackToMaximum: false);
        _state.FilterEndIndex = NormalizeRestoredFilterValue(end, fallbackToMaximum: true);

        if (_state.FilterEndIndex < _state.FilterStartIndex)
            _state.FilterEndIndex = _state.FilterStartIndex;

        RaisePropertyChanged(nameof(FilterStartIndex));
        RaisePropertyChanged(nameof(FilterEndIndex));
        RaisePropertyChanged(nameof(FilterSummary));
    }

    public void RestoreChartWindowRadius(int radius)
    {
        ChartWindowOption option = ResolveChartWindowOption(radius);
        if (Equals(_selectedChartWindow, option))
            return;

        _selectedChartWindow = option;
        _state.ChartWindowRadius = option.Radius;
        RaisePropertyChanged(nameof(SelectedChartWindow));
        RaisePropertyChanged(nameof(ChartWindowRadius));
        RaisePropertyChanged(nameof(ChartWindowSummary));
    }

    public TelemetryPoint? ApplyCurrentFilter(TelemetryPoint? preferredPoint, bool updateStatus)
    {
        if (!HasSourceData)
        {
            _state.VisiblePoints.Clear();
            ApplyVisibleData(TelemetryVisibleData.Empty);
            RaiseVisibleDataProperties();
            return null;
        }

        int start = NormalizeFilterValue(_state.FilterStartIndex, fallbackToMaximum: false);
        int end = NormalizeFilterValue(_state.FilterEndIndex, fallbackToMaximum: true);

        if (start != _state.FilterStartIndex)
        {
            _state.FilterStartIndex = start;
            RaisePropertyChanged(nameof(FilterStartIndex));
        }

        if (end != _state.FilterEndIndex)
        {
            _state.FilterEndIndex = end;
            RaisePropertyChanged(nameof(FilterEndIndex));
        }

        return RebuildVisibleData(preferredPoint, updateStatus);
    }

    private TelemetryPoint? RebuildVisibleData(TelemetryPoint? preferredPoint, bool updateStatus)
    {
        if (!HasSourceData)
            return null;

        int start = _state.FilterStartIndex;
        int end = _state.FilterEndIndex;
        TelemetryVisibleData visibleData = _dataProcessor.CreateVisibleData(_state.AllPoints, start, end);
        _state.VisiblePoints.ReplaceAll(visibleData.Points);
        ApplyVisibleData(visibleData);
        RaiseVisibleDataProperties();
        RaisePropertyChanged(nameof(FilterSummary));

        if (updateStatus)
            StatusText = $"Показан диапазон #{FilterStartIndex}–#{FilterEndIndex} · {Points.Count} точек.";

        return ResolvePreferredPoint(preferredPoint);
    }

    public int GetVisiblePositionOf(TelemetryPoint? point)
    {
        if (point is null)
            return 0;

        return _visiblePositionsByPointIndex.TryGetValue(point.Index, out int position)
            ? position
            : 0;
    }

    private void ApplyVisibleData(TelemetryVisibleData visibleData)
    {
        _visiblePositionsByPointIndex = visibleData.VisiblePositionsByPointIndex;
        _seriesSnapshot = visibleData.SeriesSnapshot;
        Statistics = visibleData.Statistics;
    }

    private void SetFilterStartIndex(int value)
    {
        int normalized = NormalizeFilterValue(value, fallbackToMaximum: false);
        if (HasSourceData && normalized > _state.FilterEndIndex)
        {
            _state.FilterEndIndex = normalized;
            RaisePropertyChanged(nameof(FilterEndIndex));
        }

        if (_state.FilterStartIndex == normalized)
            return;

        _state.FilterStartIndex = normalized;
        RaisePropertyChanged(nameof(FilterStartIndex));
        RaisePropertyChanged(nameof(FilterSummary));
    }

    private void SetFilterEndIndex(int value)
    {
        int normalized = NormalizeFilterValue(value, fallbackToMaximum: true);
        if (HasSourceData && normalized < _state.FilterStartIndex)
        {
            _state.FilterStartIndex = normalized;
            RaisePropertyChanged(nameof(FilterStartIndex));
        }

        if (_state.FilterEndIndex == normalized)
            return;

        _state.FilterEndIndex = normalized;
        RaisePropertyChanged(nameof(FilterEndIndex));
        RaisePropertyChanged(nameof(FilterSummary));
    }

    private TelemetryPoint? ResolvePreferredPoint(TelemetryPoint? preferredPoint)
    {
        if (preferredPoint is not null)
        {
            TelemetryPoint? matched = Points.FirstOrDefault(p => p.Index == preferredPoint.Index);
            if (matched is not null)
                return matched;
        }

        return Points.FirstOrDefault();
    }

    private int NormalizeFilterValue(int value, bool fallbackToMaximum)
    {
        if (!HasSourceData)
            return 0;

        int fallback = fallbackToMaximum ? _state.AllPoints.Count : 1;
        if (value <= 0)
            return fallback;

        return Math.Clamp(value, 1, _state.AllPoints.Count);
    }

    private int NormalizeRestoredFilterValue(int value, bool fallbackToMaximum)
    {
        if (!HasSourceData)
            return 0;

        if (value <= 0)
            return fallbackToMaximum ? _state.AllPoints.Count : 1;

        return Math.Clamp(value, 1, _state.AllPoints.Count);
    }

    private void RaiseSourceDataProperties()
    {
        RaisePropertyChanged(nameof(CurrentFileName));
        RaisePropertyChanged(nameof(HasSourceData));
        RaisePropertyChanged(nameof(ImportDiagnosticsText));
        RaisePropertyChanged(nameof(HasImportDiagnostics));
        RaisePropertyChanged(nameof(FilterMinimum));
        RaisePropertyChanged(nameof(FilterMaximum));
        RaisePropertyChanged(nameof(FilterStartIndex));
        RaisePropertyChanged(nameof(FilterEndIndex));
        RaisePropertyChanged(nameof(FilterSummary));
    }

    private void SetImportDiagnosticsText(string value)
    {
        if (string.Equals(_state.ImportDiagnosticsText, value, StringComparison.Ordinal))
            return;

        _state.ImportDiagnosticsText = value;
    }

    private static string BuildImportDiagnosticsText(CsvTelemetryReadResult readResult)
    {
        if (readResult.SkippedRowCount <= 0)
            return string.Empty;

        string summary = $"Пропущено {readResult.SkippedRowCount} проблемных строк при импорте.";
        if (readResult.RowIssues.Count == 0)
            return summary;

        CsvTelemetryRowIssue firstIssue = readResult.RowIssues[0];
        return $"{summary} Первая проблема: строка {firstIssue.LineNumber} ({firstIssue.Reason})";
    }

    private void RaiseVisibleDataProperties()
    {
        _visibleDataVersion++;
        RaisePropertyChanged(nameof(VisibleDataVersion));
        RaisePropertyChanged(nameof(HasPoints));
        RaisePropertyChanged(nameof(HasSourceData));
        RaisePropertyChanged(nameof(SpeedSeries));
        RaisePropertyChanged(nameof(LeanSeries));
        RaisePropertyChanged(nameof(AccelXSeries));
        RaisePropertyChanged(nameof(AccelYSeries));
        RaisePropertyChanged(nameof(AccelZSeries));
        RaisePropertyChanged(nameof(AccelSeries));
        RaisePropertyChanged(nameof(FilterSummary));
    }

    private static ChartWindowOption ResolveChartWindowOption(int radius)
        => DefaultChartWindowOptions.FirstOrDefault(option => option.Radius == radius)
            ?? DefaultChartWindowOptions.First(option => option.Radius == 1000);
}
