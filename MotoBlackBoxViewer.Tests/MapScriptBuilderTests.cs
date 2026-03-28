using MotoBlackBoxViewer.App.Services;

namespace MotoBlackBoxViewer.Tests;

public sealed class MapScriptBuilderTests
{
    [Fact]
    public void BuildSetRouteDataScript_EncodesJsonAsStringBeforeParsing()
    {
        const string routeJson = "[{\"name\":\"quote\\\"<script>\",\"value\":1}]";

        string script = MapScriptBuilder.BuildSetRouteDataScript(routeJson);

        Assert.StartsWith("window.setRouteData(JSON.parse(", script);
        Assert.Contains("\\u003Cscript\\u003E", script);
        Assert.Contains("quote", script);
        Assert.EndsWith("));", script);
    }

    [Fact]
    public void BuildBootstrapScript_EncodesPayloadSafely()
    {
        const string routeJson = "[{\"text\":\"</script>\"}]";

        string script = MapScriptBuilder.BuildBootstrapScript(routeJson);

        Assert.Contains("window.addEventListener(\"load\"", script);
        Assert.Contains("JSON.parse(", script);
        Assert.Contains("\\u003C/script\\u003E", script);
    }

    [Fact]
    public void BuildSetSelectedPointScript_HandlesNullAndCoordinates()
    {
        Assert.Equal("window.setSelectedPoint(null, null, false, false);", MapScriptBuilder.BuildSetSelectedPointScript(null, null, false, false));
        Assert.Equal("window.setSelectedPoint(43.123456, 131.654321, true, false);", MapScriptBuilder.BuildSetSelectedPointScript(43.123456, 131.654321, true, false));
    }
}
