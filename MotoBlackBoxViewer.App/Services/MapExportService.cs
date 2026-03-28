using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.App.Services;

public sealed class MapExportService : IMapExportService
{
    public string GetTemplatePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Map", "index.html");
    }

    public string BuildRouteJson(IReadOnlyList<TelemetryPoint> points)
    {
        return JsonSerializer.Serialize(points.Select(p => new
        {
            index = p.Index,
            lat = p.Latitude,
            lon = p.Longitude,
            speedKmh = p.SpeedKmh,
            leanAngleDeg = p.LeanAngleDeg,
            accelX = p.AccelX,
            accelY = p.AccelY,
            accelZ = p.AccelZ,
            distanceMeters = p.DistanceFromStartMeters
        }));
    }

    public string ExportHtml(IReadOnlyList<TelemetryPoint> points, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        string templatePath = GetTemplatePath();
        if (!File.Exists(templatePath))
            throw new FileNotFoundException("Map template was not found.", templatePath);

        string template = File.ReadAllText(templatePath, Encoding.UTF8);
        string json = BuildRouteJson(points);

        string bootstrap = MapScriptBuilder.BuildBootstrapScript(json);
        string html = template.Replace("/*__ROUTE_BOOTSTRAP__*/", bootstrap);
        string outputPath = Path.Combine(outputDirectory, $"route_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        File.WriteAllText(outputPath, html, Encoding.UTF8);

        return outputPath;
    }

    public void OpenInBrowser(string htmlPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = htmlPath,
            UseShellExecute = true
        });
    }
}
