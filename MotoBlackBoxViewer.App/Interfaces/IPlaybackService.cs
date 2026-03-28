namespace MotoBlackBoxViewer.App.Interfaces;

public interface IPlaybackService : IDisposable
{
    event EventHandler? Tick;

    bool IsRunning { get; }

    void SetInterval(TimeSpan interval);
    void Start();
    void Stop();
}
