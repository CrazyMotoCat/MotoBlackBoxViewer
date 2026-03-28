using System.Text.Json;

namespace MotoBlackBoxViewer.App.Services;

internal static class MapScriptBuilder
{
    public static string BuildSetRouteDataScript(string routeJson)
    {
        string encodedJson = JsonSerializer.Serialize(routeJson ?? "[]");
        return $"window.setRouteData(JSON.parse({encodedJson}));";
    }

    public static string BuildClearRouteDataScript() => "window.clearRouteData();";

    public static string BuildSetSelectedPointScript(
        double? latitude,
        double? longitude,
        bool isPlaybackRunning,
        bool isManualScrubbing)
    {
        return latitude.HasValue && longitude.HasValue
            ? $"window.setSelectedPoint({latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {isPlaybackRunning.ToString().ToLowerInvariant()}, {isManualScrubbing.ToString().ToLowerInvariant()});"
            : $"window.setSelectedPoint(null, null, {isPlaybackRunning.ToString().ToLowerInvariant()}, {isManualScrubbing.ToString().ToLowerInvariant()});";
    }

    public static string BuildBootstrapScript(string routeJson)
    {
        string encodedJson = JsonSerializer.Serialize(routeJson ?? "[]");
        return $"window.addEventListener(\"load\", () => setRouteData(JSON.parse({encodedJson})));";
    }
}
