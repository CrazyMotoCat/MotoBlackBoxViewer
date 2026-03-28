using System.Collections.ObjectModel;
using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.App.Models;

public sealed class TelemetrySessionState
{
    public List<TelemetryPoint> AllPoints { get; } = new();

    public ObservableCollection<TelemetryPoint> VisiblePoints { get; } = new();

    public string StatusText { get; set; } = "Готово. Откройте CSV-файл.";

    public TelemetryPoint? SelectedPoint { get; set; }

    public TelemetryStatistics Statistics { get; set; } = new();

    public string? CurrentFilePath { get; set; }

    public int PlaybackPosition { get; set; }

    public int FilterStartIndex { get; set; }

    public int FilterEndIndex { get; set; }

    public int MapRefreshVersion { get; set; }

    public bool IsRestoringSession { get; set; }

    public bool HasSourceData => AllPoints.Count > 0;

    public bool HasVisiblePoints => VisiblePoints.Count > 0;

    public string CurrentFileName => string.IsNullOrWhiteSpace(CurrentFilePath)
        ? "Файл не загружен"
        : Path.GetFileName(CurrentFilePath);
}
