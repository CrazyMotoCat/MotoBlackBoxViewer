using MotoBlackBoxViewer.App.Helpers;
using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Models;

namespace MotoBlackBoxViewer.App.ViewModels;

public sealed class TelemetryPlaybackViewModel : ObservableObject, IDisposable
{
    private readonly TelemetryDataViewModel _data;
    private readonly TelemetrySelectionViewModel _selection;
    private readonly IPlaybackCoordinator _playbackCoordinator;
    private bool _isDisposed;

    public TelemetryPlaybackViewModel(
        TelemetryDataViewModel data,
        TelemetrySelectionViewModel selection,
        IPlaybackCoordinator playbackCoordinator)
    {
        _data = data;
        _selection = selection;
        _playbackCoordinator = playbackCoordinator;

        _playbackCoordinator.Tick += PlaybackCoordinator_Tick;
    }

    public IReadOnlyList<PlaybackSpeedOption> PlaybackSpeedOptions => _playbackCoordinator.SpeedOptions;

    public PlaybackSpeedOption SelectedPlaybackSpeed
    {
        get => _playbackCoordinator.SelectedSpeed;
        set
        {
            if (!_playbackCoordinator.SetSelectedSpeed(value))
                return;

            RaisePlaybackProperties();
        }
    }

    public string PlaybackSpeedSummary => $"Скорость: {SelectedPlaybackSpeed.Label}";

    public int PlaybackIntervalMilliseconds => _playbackCoordinator.IntervalMilliseconds;

    public bool IsPlaybackRunning => _playbackCoordinator.IsRunning;

    public string PlaybackButtonText => IsPlaybackRunning ? "❚❚ Пауза" : "▶ Пуск";

    public void RestoreSpeed(string? label)
    {
        _playbackCoordinator.RestoreSpeed(label);
        RaisePlaybackProperties();
    }

    public bool Start()
    {
        if (!_data.HasPoints)
            return false;

        if (_selection.PlaybackPosition >= _selection.PlaybackMaximum)
            _selection.SelectPointByIndex(1);

        _playbackCoordinator.Start();
        RaisePlaybackProperties();
        return true;
    }

    public bool Stop()
    {
        bool wasRunning = _playbackCoordinator.IsRunning;
        if (wasRunning)
            _playbackCoordinator.Stop();

        RaisePlaybackProperties();
        return wasRunning;
    }

    public bool Toggle()
    {
        if (IsPlaybackRunning)
        {
            Stop();
            return false;
        }

        return Start();
    }

    private void PlaybackCoordinator_Tick(object? sender, EventArgs e)
    {
        if (!_data.HasPoints)
        {
            Stop();
            return;
        }

        bool moved = _selection.MoveSelection(1);
        if (!moved || _selection.PlaybackPosition >= _selection.PlaybackMaximum)
            Stop();
    }

    private void RaisePlaybackProperties()
    {
        RaisePropertyChanged(nameof(SelectedPlaybackSpeed));
        RaisePropertyChanged(nameof(PlaybackSpeedSummary));
        RaisePropertyChanged(nameof(PlaybackIntervalMilliseconds));
        RaisePropertyChanged(nameof(IsPlaybackRunning));
        RaisePropertyChanged(nameof(PlaybackButtonText));
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
