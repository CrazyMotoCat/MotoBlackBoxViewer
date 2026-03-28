using System.Collections.Specialized;
using System.ComponentModel;
using MotoBlackBoxViewer.App.Helpers;
using MotoBlackBoxViewer.App.Models;
using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.App.ViewModels;

public sealed class TelemetrySelectionViewModel : ObservableObject
{
    private readonly TelemetryDataViewModel _data;
    private readonly TelemetrySessionState _state;

    public TelemetrySelectionViewModel(TelemetryDataViewModel data, TelemetrySessionState state)
    {
        _data = data;
        _state = state;

        _data.PropertyChanged += Data_PropertyChanged;
        _data.Points.CollectionChanged += Points_CollectionChanged;
    }

    public TelemetryPoint? SelectedPoint
    {
        get => _state.SelectedPoint;
        set => SetSelectedPoint(value, syncPlayback: true);
    }

    public int? SelectedPointIndex => SelectedPoint?.Index;

    public string SelectedPointSummary
    {
        get
        {
            if (SelectedPoint is null)
                return "Точка не выбрана";

            int visiblePosition = GetVisiblePositionOf(SelectedPoint);
            return $"вид. {visiblePosition}/{_data.Points.Count} · исх. #{SelectedPoint.Index} · {SelectedPoint.Latitude:F6}, {SelectedPoint.Longitude:F6} · {SelectedPoint.SpeedKmh:F1} км/ч · наклон {SelectedPoint.LeanAngleDeg:F1}° · AX {SelectedPoint.AccelX:F2} · AY {SelectedPoint.AccelY:F2} · AZ {SelectedPoint.AccelZ:F2}";
        }
    }

    public int PlaybackMinimum => _data.HasPoints ? 1 : 0;

    public int PlaybackMaximum => _data.Points.Count;

    public int PlaybackPosition
    {
        get => _state.PlaybackPosition;
        set => SetPlaybackPosition(value, syncSelectedPoint: true);
    }

    public string PlaybackSummary
    {
        get
        {
            if (!_data.HasPoints)
                return "Нет данных";

            string sourceSuffix = SelectedPoint is null ? string.Empty : $" · исх. #{SelectedPoint.Index}";
            return $"Точка {PlaybackPosition} / {_data.Points.Count}{sourceSuffix}";
        }
    }

    public void Clear()
    {
        _state.SelectedPoint = null;
        _state.PlaybackPosition = 0;
        RaiseSelectionProperties();
    }

    public void SynchronizeWithVisiblePoints(TelemetryPoint? preferredPoint)
    {
        if (!_data.HasPoints)
        {
            Clear();
            return;
        }

        TelemetryPoint? nextPoint = null;
        if (preferredPoint is not null)
            nextPoint = _data.Points.FirstOrDefault(p => p.Index == preferredPoint.Index);

        nextPoint ??= _data.Points.FirstOrDefault();
        SetSelectedPoint(nextPoint, syncPlayback: true);
    }

    public void SelectPointByIndex(int oneBasedIndex)
        => SetPlaybackPosition(oneBasedIndex, syncSelectedPoint: true);

    public bool MoveSelection(int delta)
    {
        if (!_data.HasPoints)
            return false;

        int target = PlaybackPosition <= 0 ? 1 : PlaybackPosition + delta;
        int clamped = Math.Clamp(target, 1, PlaybackMaximum);
        bool changed = clamped != PlaybackPosition;
        SetPlaybackPosition(clamped, syncSelectedPoint: true);
        return changed;
    }

    private void SetSelectedPoint(TelemetryPoint? value, bool syncPlayback)
    {
        if (ReferenceEquals(_state.SelectedPoint, value) || Equals(_state.SelectedPoint, value))
            return;

        _state.SelectedPoint = value;
        RaiseSelectionProperties();

        if (syncPlayback)
        {
            int targetPosition = GetVisiblePositionOf(value);
            SetPlaybackPosition(targetPosition, syncSelectedPoint: false);
        }
    }

    private void SetPlaybackPosition(int value, bool syncSelectedPoint)
    {
        int clamped = !_data.HasPoints ? 0 : Math.Clamp(value, 1, PlaybackMaximum);
        if (_state.PlaybackPosition == clamped)
            return;

        _state.PlaybackPosition = clamped;
        RaisePropertyChanged(nameof(PlaybackPosition));
        RaisePropertyChanged(nameof(PlaybackSummary));

        if (syncSelectedPoint)
        {
            TelemetryPoint? point = clamped == 0 ? null : _data.Points[clamped - 1];
            SetSelectedPoint(point, syncPlayback: false);
        }
    }

    private int GetVisiblePositionOf(TelemetryPoint? point)
        => _data.GetVisiblePositionOf(point);

    private void Data_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TelemetryDataViewModel.HasPoints) or nameof(TelemetryDataViewModel.FilterSummary))
            RaiseSelectionProperties();
    }

    private void Points_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RaiseSelectionProperties();

    private void RaiseSelectionProperties()
    {
        RaisePropertyChanged(nameof(SelectedPoint));
        RaisePropertyChanged(nameof(SelectedPointIndex));
        RaisePropertyChanged(nameof(SelectedPointSummary));
        RaisePropertyChanged(nameof(PlaybackMinimum));
        RaisePropertyChanged(nameof(PlaybackMaximum));
        RaisePropertyChanged(nameof(PlaybackPosition));
        RaisePropertyChanged(nameof(PlaybackSummary));
    }
}
