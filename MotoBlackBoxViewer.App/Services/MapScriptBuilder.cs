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

    public static string BuildSetSelectedIndexScript(int? selectedPointIndex)
    {
        return selectedPointIndex.HasValue
            ? $"window.setSelectedIndex({selectedPointIndex.Value});"
            : "window.setSelectedIndex(null);";
    }

    public static string BuildBootstrapScript(string routeJson)
    {
        string encodedJson = JsonSerializer.Serialize(routeJson ?? "[]");
        return $"window.addEventListener(\"load\", () => setRouteData(JSON.parse({encodedJson})));";
    }
}
