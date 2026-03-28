using System.Collections.ObjectModel;
using MotoBlackBoxViewer.App.Helpers;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.Core.Interfaces;
using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.App.ViewModels;

public sealed class TelemetryDataViewModel : ObservableObject
{
    private readonly ICsvTelemetryReader _reader;
    private readonly ITelemetryAnalyzer _analyzer;
    private readonly TelemetrySessionState _state;
    private TelemetrySeriesSnapshot _seriesSnapshot = TelemetrySeriesSnapshot.Empty;
    private int _visibleDataVersion;

    public TelemetryDataViewModel(
        ICsvTelemetryReader reader,
        ITelemetryAnalyzer analyzer,
        TelemetrySessionState state)
    {
        _reader = reader;
        _analyzer = analyzer;
        _state = state;
    }

    public ObservableCollection<TelemetryPoint> Points => _state.VisiblePoints;

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
        IReadOnlyList<TelemetryPoint> points = await _reader.ReadAsync(filePath, cancellationToken);

        _state.AllPoints.Clear();
        _state.AllPoints.AddRange(points);
        _state.CurrentFilePath = filePath;
        _state.FilterStartIndex = _state.AllPoints.Count == 0 ? 0 : 1;
        _state.FilterEndIndex = _state.AllPoints.Count;

        RaiseSourceDataProperties();
    }

    public void Clear()
    {
        _state.AllPoints.Clear();
        _state.VisiblePoints.Clear();
        _state.CurrentFilePath = null;
        _state.FilterStartIndex = 0;
        _state.FilterEndIndex = 0;
        _seriesSnapshot = TelemetrySeriesSnapshot.Empty;
        Statistics = new TelemetryStatistics();
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

    public TelemetryPoint? ApplyCurrentFilter(TelemetryPoint? preferredPoint, bool updateStatus)
    {
        _state.VisiblePoints.Clear();

        if (!HasSourceData)
        {
            _seriesSnapshot = TelemetrySeriesSnapshot.Empty;
            Statistics = new TelemetryStatistics();
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

        List<TelemetryPoint> filtered = _state.AllPoints
            .Where(p => p.Index >= start && p.Index <= end)
            .ToList();

        foreach (TelemetryPoint point in filtered)
            _state.VisiblePoints.Add(point);

        _seriesSnapshot = TelemetrySeriesSnapshot.Create(filtered);
        Statistics = _analyzer.Analyze(filtered);
        RaiseVisibleDataProperties();
        RaisePropertyChanged(nameof(FilterSummary));

        if (updateStatus)
            StatusText = $"Применен диапазон #{FilterStartIndex}–#{FilterEndIndex}. Показано {Points.Count} точек.";

        return ResolvePreferredPoint(preferredPoint);
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
        RaisePropertyChanged(nameof(FilterMinimum));
        RaisePropertyChanged(nameof(FilterMaximum));
        RaisePropertyChanged(nameof(FilterStartIndex));
        RaisePropertyChanged(nameof(FilterEndIndex));
        RaisePropertyChanged(nameof(FilterSummary));
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
}
