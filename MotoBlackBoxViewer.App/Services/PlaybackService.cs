using System.Windows.Threading;

namespace MotoBlackBoxViewer.App.Services;

public sealed class PlaybackService : IDisposable
{
    private readonly DispatcherTimer _timer;

    public PlaybackService()
    {
        _timer = new DispatcherTimer();
        _timer.Tick += (_, _) => Tick?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Tick;

    public bool IsRunning => _timer.IsEnabled;

    public void SetInterval(TimeSpan interval)
    {
        _timer.Interval = interval < TimeSpan.FromMilliseconds(50)
            ? TimeSpan.FromMilliseconds(50)
            : interval;
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    public void Dispose() => _timer.Stop();
}
