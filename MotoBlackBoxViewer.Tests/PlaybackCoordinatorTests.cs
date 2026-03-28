using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Services;

namespace MotoBlackBoxViewer.Tests;

public sealed class PlaybackCoordinatorTests
{
    [Fact]
    public void SpeedOptions_Include8xAnd16x()
    {
        using PlaybackCoordinator coordinator = new(new StubPlaybackService());

        Assert.Contains(coordinator.SpeedOptions, option => option.Label == "8x" && option.Multiplier == 8.0);
        Assert.Contains(coordinator.SpeedOptions, option => option.Label == "16x" && option.Multiplier == 16.0);
    }

    [Fact]
    public void RestoreSpeed_Applies16xAndUpdatesInterval()
    {
        StubPlaybackService playbackService = new();
        using PlaybackCoordinator coordinator = new(playbackService);

        coordinator.RestoreSpeed("16x");

        Assert.Equal("16x", coordinator.SelectedSpeed.Label);
        Assert.Equal(22, coordinator.IntervalMilliseconds);
        Assert.Equal(TimeSpan.FromMilliseconds(22), playbackService.LastInterval);
    }

    private sealed class StubPlaybackService : IPlaybackService
    {
        public event EventHandler? Tick
        {
            add { }
            remove { }
        }

        public bool IsRunning { get; private set; }

        public TimeSpan LastInterval { get; private set; }

        public void SetInterval(TimeSpan interval)
        {
            LastInterval = interval;
        }

        public void Start()
        {
            IsRunning = true;
        }

        public void Stop()
        {
            IsRunning = false;
        }

        public void Dispose()
        {
        }
    }
}
