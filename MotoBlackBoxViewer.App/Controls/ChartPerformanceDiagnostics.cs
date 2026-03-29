using System.Diagnostics;

namespace MotoBlackBoxViewer.App.Controls;

internal static class ChartPerformanceDiagnostics
{
    private static readonly object SyncRoot = new();
    private static Action<ChartPerformanceEvent>? _listener;

    public static IDisposable PushListener(Action<ChartPerformanceEvent> listener)
    {
        lock (SyncRoot)
        {
            _listener += listener;
        }

        return new ListenerScope(listener);
    }

    public static void Report(
        string operation,
        int inputPointCount,
        int outputPointCount,
        TimeSpan elapsed,
        string? detail = null,
        int largePointThreshold = 5000,
        double slowMillisecondsThreshold = 8d)
    {
        if (inputPointCount < largePointThreshold && elapsed.TotalMilliseconds < slowMillisecondsThreshold)
            return;

        ChartPerformanceEvent evt = new(
            operation,
            inputPointCount,
            outputPointCount,
            elapsed,
            detail ?? string.Empty);

        Trace.TraceInformation(
            $"[ChartPerf] {evt.Operation} input={evt.InputPointCount} output={evt.OutputPointCount} elapsedMs={evt.Elapsed.TotalMilliseconds:F2} detail={evt.Detail}");

        Action<ChartPerformanceEvent>? listener;
        lock (SyncRoot)
        {
            listener = _listener;
        }

        listener?.Invoke(evt);
    }

    private sealed class ListenerScope(Action<ChartPerformanceEvent> listener) : IDisposable
    {
        private readonly Action<ChartPerformanceEvent> _registeredListener = listener;
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            lock (SyncRoot)
            {
                _listener -= _registeredListener;
            }
        }
    }
}

internal readonly record struct ChartPerformanceEvent(
    string Operation,
    int InputPointCount,
    int OutputPointCount,
    TimeSpan Elapsed,
    string Detail);
