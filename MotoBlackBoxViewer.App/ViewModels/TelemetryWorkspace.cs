using System.Collections.ObjectModel;
using MotoBlackBoxViewer.App.Helpers;
using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.Core.Interfaces;
using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.App.ViewModels;

public sealed class TelemetryWorkspace : ObservableObject, IDisposable
{
    private readonly ICsvTelemetryReader _reader;
    private readonly ITelemetryAnalyzer _analyzer;
    private readonly IMapExportService _mapExportService;
    private readonly IPlaybackCoordinator _playbackCoordinator;
    private readonly ISessionPersistenceCoordinator _sessionPersistenceCoordinator;
    private readonly TelemetrySessionState _state = new();
    private bool _isDisposed;

    public TelemetryWorkspace(
        ICsvTelemetryReader reader,
        ITelemetryAnalyzer analyzer,
        IMapExportService mapExportService,
        IPlaybackCoordinator playbackCoordinator,
        ISessionPersistenceCoordinator sessionPersistenceCoordinator)
    {
        _reader = reader;
        _analyzer = analyzer;
        _mapExportService = mapExportService;
        _playbackCoordinator = playbackCoordinator;
        _sessionPersistenceCoordinator = sessionPersistenceCoordinator;

        _playbackCoordinator.Tick += PlaybackCoordinator_Tick;
    }

    public IReadOnlyList<PlaybackSpeedOption> PlaybackSpeedOptions => _playbackCoordinator.SpeedOptions;

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

    public TelemetryPoint? SelectedPoint
    {
        get => _state.SelectedPoint;
        set => SetSelectedPoint(value, syncPlayback: true);
    }

    public int? SelectedPointIndex => SelectedPoint?.Index;

    public string RouteJson => _mapExportService.BuildRouteJson(Points);

    public int MapRefreshVersion
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
        get => _state.Statistics;
        private set
        {
            _state.Statistics = value;
            RaisePropertyChanged();
        }
    }

    public IReadOnlyList<double> SpeedSeries => Points.Select(p => p.SpeedKmh).ToArray();
    public IReadOnlyList<double> LeanSeries => Points.Select(p => p.LeanAngleDeg).ToArray();
    public IReadOnlyList<double> AccelXSeries => Points.Select(p => p.AccelX).ToArray();
    public IReadOnlyList<double> AccelYSeries => Points.Select(p => p.AccelY).ToArray();
    public IReadOnlyList<double> AccelZSeries => Points.Select(p => p.AccelZ).ToArray();

    public IReadOnlyList<ChartSeriesDefinition> AccelSeries =>
    [
        new ChartSeriesDefinition("Accel X", AccelXSeries, "#22C55E"),
        new ChartSeriesDefinition("Accel Y", AccelYSeries, "#F59E0B"),
        new ChartSeriesDefinition("Accel Z", AccelZSeries, "#EF4444")
    ];

    public int PlaybackMinimum => HasPoints ? 1 : 0;
    public int PlaybackMaximum => Points.Count;

    public int PlaybackPosition
    {
        get => _state.PlaybackPosition;
        set => SetPlaybackPosition(value, syncSelectedPoint: true);
    }

    public bool IsPlaybackRunning => _playbackCoordinator.IsRunning;

    public string PlaybackButtonText => IsPlaybackRunning ? "❚❚ Пауза" : "▶ Пуск";

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
    public int FilterMaximum => _state.AllPoints.Count;

    public int FilterStartIndex
    {
        get => _state.FilterStartIndex;
        set
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
            RaisePropertyChanged();
            ApplyFilter(preserveSelection: true, updateStatus: true);
        }
    }

    public int FilterEndIndex
    {
        get => _state.FilterEndIndex;
        set
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
            RaisePropertyChanged();
            ApplyFilter(preserveSelection: true, updateStatus: true);
        }
    }

    public string FilterSummary => !HasSourceData
        ? "Фильтр недоступен"
        : $"Диапазон #{FilterStartIndex}–#{FilterEndIndex} · показано {Points.Count} из {_state.AllPoints.Count} точек";

    public PlaybackSpeedOption SelectedPlaybackSpeed
    {
        get => _playbackCoordinator.SelectedSpeed;
        set
        {
            if (!_playbackCoordinator.SetSelectedSpeed(value))
                return;

            RaisePropertyChanged();
            RaisePropertyChanged(nameof(PlaybackSpeedSummary));
            RaisePropertyChanged(nameof(PlaybackIntervalMilliseconds));
            PersistSession(includeSelectedPosition: false);

            if (IsPlaybackRunning)
                StatusText = $"Скорость воспроизведения изменена: {SelectedPlaybackSpeed.Label}.";
        }
    }

    public string PlaybackSpeedSummary => $"Скорость: {SelectedPlaybackSpeed.Label}";

    public int PlaybackIntervalMilliseconds => _playbackCoordinator.IntervalMilliseconds;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        AppSessionSettings session = _sessionPersistenceCoordinator.Load();
        _playbackCoordinator.RestoreSpeed(session.SelectedPlaybackSpeedLabel);
        RaisePropertyChanged(nameof(SelectedPlaybackSpeed));
        RaisePropertyChanged(nameof(PlaybackSpeedSummary));
        RaisePropertyChanged(nameof(PlaybackIntervalMilliseconds));

        if (string.IsNullOrWhiteSpace(session.LastFilePath))
            return;

        if (!File.Exists(session.LastFilePath))
        {
            StatusText = $"Последний файл не найден: {session.LastFilePath}";
            return;
        }

        try
        {
            _state.IsRestoringSession = true;
            await LoadCsvAsync(session.LastFilePath, cancellationToken);

            if (HasSourceData)
            {
                _state.FilterStartIndex = NormalizeRestoredFilterValue(session.FilterStartIndex, fallbackToMaximum: false);
                _state.FilterEndIndex = NormalizeRestoredFilterValue(session.FilterEndIndex, fallbackToMaximum: true);

                if (_state.FilterEndIndex < _state.FilterStartIndex)
                    _state.FilterEndIndex = _state.FilterStartIndex;

                RaisePropertyChanged(nameof(FilterStartIndex));
                RaisePropertyChanged(nameof(FilterEndIndex));
                ApplyFilter(preserveSelection: false, updateStatus: false);

                int restoredPosition = session.SelectedVisiblePosition <= 0
                    ? 1
                    : Math.Clamp(session.SelectedVisiblePosition, 1, PlaybackMaximum);

                SetPlaybackPosition(restoredPosition, syncSelectedPoint: true);
                RequestMapRefresh();
                StatusText = $"Восстановлена последняя сессия: {Path.GetFileName(session.LastFilePath)}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Не удалось восстановить последнюю сессию: {ex.Message}";
        }
        finally
        {
            _state.IsRestoringSession = false;
        }
    }

    public void SaveSession() => PersistSession(includeSelectedPosition: true);

    public async Task LoadCsvAsync(string filePath, CancellationToken cancellationToken = default)
    {
        StatusText = "Читаю CSV...";

        IReadOnlyList<TelemetryPoint> points = await _reader.ReadAsync(filePath, cancellationToken);

        _state.AllPoints.Clear();
        _state.AllPoints.AddRange(points);
        _state.CurrentFilePath = filePath;
        _state.FilterStartIndex = _state.AllPoints.Count == 0 ? 0 : 1;
        _state.FilterEndIndex = _state.AllPoints.Count;

        RaisePropertyChanged(nameof(CurrentFileName));
        RaisePropertyChanged(nameof(FilterMinimum));
        RaisePropertyChanged(nameof(FilterMaximum));
        RaisePropertyChanged(nameof(FilterStartIndex));
        RaisePropertyChanged(nameof(FilterEndIndex));

        ApplyFilter(preserveSelection: false, updateStatus: false);
        StatusText = $"Загружено точек: {_state.AllPoints.Count}. Показано: {Points.Count}. Файл: {Path.GetFileName(filePath)}";
        PersistSession(includeSelectedPosition: false);
    }

    public void Clear()
    {
        StopPlayback(updateStatus: false);

        _state.AllPoints.Clear();
        _state.VisiblePoints.Clear();
        _state.CurrentFilePath = null;
        _state.FilterStartIndex = 0;
        _state.FilterEndIndex = 0;
        Statistics = new TelemetryStatistics();

        RaiseCollectionDependentProperties();
        RaisePropertyChanged(nameof(CurrentFileName));
        RaisePropertyChanged(nameof(FilterMinimum));
        RaisePropertyChanged(nameof(FilterMaximum));
        RaisePropertyChanged(nameof(FilterStartIndex));
        RaisePropertyChanged(nameof(FilterEndIndex));
        RaisePropertyChanged(nameof(FilterSummary));

        SetSelectedPoint(null, syncPlayback: true);
        RequestMapRefresh();
        StatusText = "Данные очищены.";
        PersistSession(includeSelectedPosition: false);
    }

    public void ResetFilter()
    {
        if (!HasSourceData)
            return;

        StopPlayback(updateStatus: false);
        _state.FilterStartIndex = 1;
        _state.FilterEndIndex = _state.AllPoints.Count;
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

    public void RequestMapRefresh() => MapRefreshVersion++;

    public void TogglePlayback()
    {
        if (!HasPoints)
            return;

        if (IsPlaybackRunning)
        {
            StopPlayback();
            return;
        }

        StartPlayback();
    }

    public void StartPlayback()
    {
        if (!HasPoints)
            return;

        if (PlaybackPosition >= PlaybackMaximum)
            SelectPointByIndex(1);

        _playbackCoordinator.Start();
        RaisePlaybackStateProperties();
        StatusText = $"Воспроизведение маршрута запущено ({SelectedPlaybackSpeed.Label}).";
    }

    public void StopPlayback(bool updateStatus = true)
    {
        if (!_playbackCoordinator.IsRunning)
        {
            RaisePlaybackStateProperties();
            return;
        }

        _playbackCoordinator.Stop();
        RaisePlaybackStateProperties();

        if (updateStatus)
            StatusText = "Воспроизведение остановлено.";
    }

    private void PlaybackCoordinator_Tick(object? sender, EventArgs e)
    {
        if (!HasPoints)
        {
            StopPlayback(updateStatus: false);
            return;
        }

        bool moved = MoveSelection(1);
        if (!moved || PlaybackPosition >= PlaybackMaximum)
            StopPlayback(updateStatus: false);
    }

    private void ApplyFilter(bool preserveSelection, bool updateStatus)
    {
        StopPlayback(updateStatus: false);
        TelemetryPoint? preferredPoint = preserveSelection ? SelectedPoint : null;

        _state.VisiblePoints.Clear();

        if (!HasSourceData)
        {
            Statistics = new TelemetryStatistics();
            RaiseCollectionDependentProperties();
            SetSelectedPoint(null, syncPlayback: true);
            RequestMapRefresh();
            PersistSession(includeSelectedPosition: false);
            return;
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

        Statistics = _analyzer.Analyze(filtered);
        RaiseCollectionDependentProperties();

        TelemetryPoint? nextPoint = null;
        if (preferredPoint is not null)
            nextPoint = Points.FirstOrDefault(p => p.Index == preferredPoint.Index);

        nextPoint ??= Points.FirstOrDefault();
        SetSelectedPoint(nextPoint, syncPlayback: true);
        RaisePropertyChanged(nameof(FilterSummary));
        RequestMapRefresh();

        if (updateStatus)
            StatusText = $"Применен диапазон #{FilterStartIndex}–#{FilterEndIndex}. Показано {Points.Count} точек.";

        PersistSession(includeSelectedPosition: false);
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
        RaisePropertyChanged(nameof(AccelSeries));
        RaisePropertyChanged(nameof(RouteJson));
        RaisePropertyChanged(nameof(PlaybackMinimum));
        RaisePropertyChanged(nameof(PlaybackMaximum));
        RaisePropertyChanged(nameof(PlaybackSummary));
        RaisePropertyChanged(nameof(SelectedPointSummary));
        RaisePropertyChanged(nameof(FilterSummary));
        RaisePlaybackStateProperties();
    }

    private void RaisePlaybackStateProperties()
    {
        RaisePropertyChanged(nameof(IsPlaybackRunning));
        RaisePropertyChanged(nameof(PlaybackButtonText));
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

    private void SetSelectedPoint(TelemetryPoint? value, bool syncPlayback)
    {
        if (ReferenceEquals(_state.SelectedPoint, value) || Equals(_state.SelectedPoint, value))
            return;

        _state.SelectedPoint = value;
        RaisePropertyChanged(nameof(SelectedPoint));
        RaisePropertyChanged(nameof(SelectedPointSummary));
        RaisePropertyChanged(nameof(PlaybackSummary));
        RaisePropertyChanged(nameof(SelectedPointIndex));

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
        if (_state.PlaybackPosition == clamped)
            return;

        _state.PlaybackPosition = clamped;
        RaisePropertyChanged(nameof(PlaybackPosition));
        RaisePropertyChanged(nameof(PlaybackSummary));

        if (syncSelectedPoint)
        {
            TelemetryPoint? point = clamped == 0 ? null : Points[clamped - 1];
            SetSelectedPoint(point, syncPlayback: false);
        }
    }

    private void PersistSession(bool includeSelectedPosition)
    {
        if (_state.IsRestoringSession)
            return;

        _sessionPersistenceCoordinator.Save(_state, SelectedPlaybackSpeed.Label, includeSelectedPosition);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _playbackCoordinator.Tick -= PlaybackCoordinator_Tick;
        _playbackCoordinator.Dispose();
    }
}
