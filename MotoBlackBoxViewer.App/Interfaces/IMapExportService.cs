using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.App.Interfaces;

public interface IMapExportService
{
    string GetTemplatePath();
    string BuildRouteJson(IReadOnlyList<TelemetryPoint> points);
    string ExportHtml(IReadOnlyList<TelemetryPoint> points, string outputDirectory);
    void OpenInBrowser(string htmlPath);
}
