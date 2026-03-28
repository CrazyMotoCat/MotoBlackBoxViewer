namespace MotoBlackBoxViewer.App.Models;

public sealed record ChartWindowOption(string Label, int Radius)
{
    public bool IsFullRange => Radius <= 0;
}
