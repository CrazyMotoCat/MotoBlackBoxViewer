using MotoBlackBoxViewer.Core.Models;

namespace MotoBlackBoxViewer.App.Services;

internal static class MapRouteSimplifier
{
    public static IReadOnlyList<TelemetryPoint> Simplify(IReadOnlyList<TelemetryPoint> points, int maxPointCount)
    {
        if (points.Count <= maxPointCount || maxPointCount < 3)
            return points;

        List<TelemetryPoint> simplified = new(maxPointCount);
        int previousIndex = -1;
        int lastSourceIndex = points.Count - 1;

        for (int i = 0; i < maxPointCount; i++)
        {
            int sourceIndex = (int)Math.Round(i * lastSourceIndex / (double)(maxPointCount - 1));
            if (sourceIndex == previousIndex)
                continue;

            simplified.Add(points[sourceIndex]);
            previousIndex = sourceIndex;
        }

        if (simplified[^1].Index != points[^1].Index)
            simplified.Add(points[^1]);

        return simplified;
    }
}
