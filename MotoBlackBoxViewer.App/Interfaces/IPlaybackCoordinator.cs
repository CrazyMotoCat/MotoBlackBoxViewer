using MotoBlackBoxViewer.App.Models;

namespace MotoBlackBoxViewer.App.Interfaces;

public interface IPlaybackCoordinator : IDisposable
{
    event EventHandler? Tick;

    IReadOnlyList<PlaybackSpeedOption> SpeedOptions { get; }

    PlaybackSpeedOption SelectedSpeed { get; }

    int IntervalMilliseconds { get; }

    bool IsRunning { get; }

    bool SetSelectedSpeed(PlaybackSpeedOption option);

    void RestoreSpeed(string? label);

    void Start();

    void Stop();
}
