namespace MotoBlackBoxViewer.App.Models;

public sealed class AppSessionSettings
{
    public string? LastFilePath { get; set; }
    public int FilterStartIndex { get; set; }
    public int FilterEndIndex { get; set; }
    public string? SelectedPlaybackSpeedLabel { get; set; }
    public int SelectedVisiblePosition { get; set; }
}
