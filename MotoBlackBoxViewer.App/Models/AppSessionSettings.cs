namespace MotoBlackBoxViewer.App.Models;

public sealed class AppSessionSettings
{
    public string? LastFilePath { get; set; }
    public int FilterStartIndex { get; set; }
    public int FilterEndIndex { get; set; }
    public int SelectedChartWindowRadius { get; set; } = 1000;
    public bool IsChartProfilingEnabled { get; set; }
    public string? SelectedPlaybackSpeedLabel { get; set; }
    public int SelectedVisiblePosition { get; set; }
}
