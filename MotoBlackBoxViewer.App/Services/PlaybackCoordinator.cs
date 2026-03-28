using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Models;

namespace MotoBlackBoxViewer.App.Services;

public sealed class PlaybackCoordinator : IPlaybackCoordinator
{
    private readonly IPlaybackService _playbackService;

    public PlaybackCoordinator(IPlaybackService playbackService)
    {
        _playbackService = playbackService;
        SpeedOptions = new[]
        {
            new PlaybackSpeedOption("0.25x", 0.25),
            new PlaybackSpeedOption("0.5x", 0.5),
            new PlaybackSpeedOption("1x", 1.0),
            new PlaybackSpeedOption("2x", 2.0),
            new PlaybackSpeedOption("4x", 4.0),
            new PlaybackSpeedOption("8x", 8.0),
            new PlaybackSpeedOption("16x", 16.0)
        };

        SelectedSpeed = SpeedOptions[2];
        _playbackService.Tick += PlaybackService_Tick;
        _playbackService.SetInterval(TimeSpan.FromMilliseconds(IntervalMilliseconds));
    }

    public event EventHandler? Tick;

    public IReadOnlyList<PlaybackSpeedOption> SpeedOptions { get; }

    public PlaybackSpeedOption SelectedSpeed { get; private set; }

    public int IntervalMilliseconds => (int)Math.Round(350d / SelectedSpeed.Multiplier);

    public bool IsRunning => _playbackService.IsRunning;

    public bool SetSelectedSpeed(PlaybackSpeedOption option)
    {
        if (option is null || ReferenceEquals(option, SelectedSpeed) || Equals(option, SelectedSpeed))
            return false;

        SelectedSpeed = option;
        _playbackService.SetInterval(TimeSpan.FromMilliseconds(IntervalMilliseconds));
        return true;
    }

    public void RestoreSpeed(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return;

        PlaybackSpeedOption? match = SpeedOptions.FirstOrDefault(option =>
            string.Equals(option.Label, label, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            return;

        SelectedSpeed = match;
        _playbackService.SetInterval(TimeSpan.FromMilliseconds(IntervalMilliseconds));
    }

    public void Start()
    {
        _playbackService.SetInterval(TimeSpan.FromMilliseconds(IntervalMilliseconds));
        _playbackService.Start();
    }

    public void Stop() => _playbackService.Stop();

    private void PlaybackService_Tick(object? sender, EventArgs e)
        => Tick?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _playbackService.Tick -= PlaybackService_Tick;
        _playbackService.Dispose();
    }
}
