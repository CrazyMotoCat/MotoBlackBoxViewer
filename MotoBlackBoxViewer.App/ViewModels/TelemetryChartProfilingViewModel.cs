using MotoBlackBoxViewer.App.Controls;
using MotoBlackBoxViewer.App.Helpers;
using MotoBlackBoxViewer.App.Models;

namespace MotoBlackBoxViewer.App.ViewModels;

public sealed class TelemetryChartProfilingViewModel : ObservableObject, IDisposable
{
    private readonly TelemetrySessionState _state;
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, Aggregate> _aggregates = new(StringComparer.Ordinal);
    private IReadOnlyList<ChartProfilingOperationSummary> _rows = Array.Empty<ChartProfilingOperationSummary>();
    private IDisposable? _listenerScope;
    private bool _isDisposed;

    public TelemetryChartProfilingViewModel(TelemetrySessionState state)
    {
        _state = state;

        if (_state.IsChartProfilingEnabled)
            StartProfilingSession();
    }

    public bool IsEnabled
    {
        get => _state.IsChartProfilingEnabled;
        set
        {
            if (_state.IsChartProfilingEnabled == value)
                return;

            _state.IsChartProfilingEnabled = value;
            if (value)
                StartProfilingSession();
            else
                StopProfilingSession(clearData: true);

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ToggleButtonText));
            RaisePropertyChanged(nameof(SummaryText));
            RaisePropertyChanged(nameof(HasData));
            RaisePropertyChanged(nameof(Rows));
        }
    }

    public string ToggleButtonText => IsEnabled
        ? "Профилинг: вкл"
        : "Профилинг: выкл";

    public IReadOnlyList<ChartProfilingOperationSummary> Rows => _rows;

    public bool HasData => _rows.Count > 0;

    public string SummaryText
    {
        get
        {
            if (!IsEnabled)
                return "Режим выключен.";

            if (!HasData)
                return "Собираем тайминги chart pipeline с момента включения.";

            int totalSamples = _rows.Sum(row => row.Samples);
            double totalMilliseconds = _rows.Sum(row => row.TotalMilliseconds);
            ChartProfilingOperationSummary hottestStage = _rows[0];
            return $"Сэмплов: {totalSamples} · суммарно {totalMilliseconds:F1} ms · hottest {hottestStage.Stage}: {hottestStage.TotalMilliseconds:F1} ms.";
        }
    }

    public void RestoreEnabled(bool isEnabled) => IsEnabled = isEnabled;

    private void StartProfilingSession()
    {
        StopProfilingSession(clearData: true);
        _listenerScope = ChartPerformanceDiagnostics.PushListener(HandlePerformanceEvent);
    }

    private void StopProfilingSession(bool clearData)
    {
        _listenerScope?.Dispose();
        _listenerScope = null;

        if (clearData)
            ClearAggregates();
    }

    private void ClearAggregates()
    {
        lock (_syncRoot)
        {
            _aggregates.Clear();
            _rows = Array.Empty<ChartProfilingOperationSummary>();
        }

        RaisePropertyChanged(nameof(Rows));
        RaisePropertyChanged(nameof(HasData));
        RaisePropertyChanged(nameof(SummaryText));
    }

    private void HandlePerformanceEvent(ChartPerformanceEvent evt)
    {
        IReadOnlyList<ChartProfilingOperationSummary> nextRows;

        lock (_syncRoot)
        {
            (string key, string displayName) = Classify(evt.Operation);
            if (!_aggregates.TryGetValue(key, out Aggregate? aggregate))
            {
                aggregate = new Aggregate(displayName);
                _aggregates.Add(key, aggregate);
            }

            aggregate.Add(evt);
            nextRows = _aggregates.Values
                .OrderByDescending(static aggregateItem => aggregateItem.TotalElapsed)
                .ThenBy(static aggregateItem => aggregateItem.DisplayName, StringComparer.Ordinal)
                .Select(static aggregateItem => aggregateItem.ToSummary())
                .ToArray();
            _rows = nextRows;
        }

        RaisePropertyChanged(nameof(Rows));
        RaisePropertyChanged(nameof(HasData));
        RaisePropertyChanged(nameof(SummaryText));
    }

    private static (string Key, string DisplayName) Classify(string operation)
        => operation switch
        {
            "CreateVisibleData" => ("CreateVisibleData", "CreateVisibleData"),
            "SliceValues" or "SliceSeries" => ("ChartSlicing", "Chart slicing"),
            "DownsampleValues" or "DownsampleSeries" => ("Downsampling", "Downsampling"),
            "RedrawSingleSeries" or "RedrawMultiSeries" => ("Redraw", "Redraw"),
            _ => (operation, operation)
        };

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        StopProfilingSession(clearData: false);
    }

    private sealed class Aggregate
    {
        public Aggregate(string displayName)
        {
            DisplayName = displayName;
        }

        public string DisplayName { get; }

        public int Samples { get; private set; }

        public TimeSpan TotalElapsed { get; private set; }

        public TimeSpan MaxElapsed { get; private set; }

        public int LastInputPointCount { get; private set; }

        public int LastOutputPointCount { get; private set; }

        public string LastOperation { get; private set; } = string.Empty;

        public string LastDetail { get; private set; } = string.Empty;

        public void Add(ChartPerformanceEvent evt)
        {
            Samples++;
            TotalElapsed += evt.Elapsed;
            if (evt.Elapsed > MaxElapsed)
                MaxElapsed = evt.Elapsed;

            LastInputPointCount = evt.InputPointCount;
            LastOutputPointCount = evt.OutputPointCount;
            LastOperation = evt.Operation;
            LastDetail = evt.Detail;
        }

        public ChartProfilingOperationSummary ToSummary()
        {
            double totalMilliseconds = TotalElapsed.TotalMilliseconds;
            return new ChartProfilingOperationSummary(
                DisplayName,
                Samples,
                totalMilliseconds,
                Samples == 0 ? 0d : totalMilliseconds / Samples,
                MaxElapsed.TotalMilliseconds,
                LastInputPointCount,
                LastOutputPointCount,
                LastOperation,
                LastDetail);
        }
    }
}
