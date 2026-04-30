// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using LiveChartsCore.Drawing;
using LiveChartsCore.Drawing.Segments;
using LiveChartsCore.Painting;

namespace LiveChartsCore.Console.Drawing.Geometries;

/// <summary>
/// Stroke-only line through the chart data points. Each cubic segment is sampled into a polyline
/// and rasterized via Bresenham. Fill (closing-to-pivot area) is intentionally not implemented —
/// it would need a scanline filler we don't have, and is a poor fit for a single-cell-color grid.
/// </summary>
public class VectorAreaGeometry : BaseVectorGeometry, IDrawnElement<ConsoleDrawingContext>
{
    private const int SamplesPerSegment = 16;

    public void Draw(ConsoleDrawingContext context)
    {
        if (Commands.Count == 0) return;
        var paint = context.ActiveLvcPaint;
        if (paint is null) return;
        if (!paint.PaintStyle.HasFlag(PaintStyle.Stroke)) return;

        var color = context.ActiveColor;

        foreach (var segment in Commands)
        {
            segment.IsValid = true;

            if (segment is CubicBezierSegment cubic)
                StrokeCubic(context, cubic, color);
            else
                context.Surface.DrawLine(
                    (int)segment.Xi, (int)segment.Yi,
                    (int)segment.Xj, (int)segment.Yj, color);
        }
    }

    private static void StrokeCubic(ConsoleDrawingContext context, CubicBezierSegment seg, LvcColor color)
    {
        // The chart engine uses a Catmull-Rom-derived control point pair (Xi/Yi → Xj/Yj with Xm/Ym
        // being the second control). Sample the cubic uniformly and stroke the polyline.
        var x0 = seg.Xi;  var y0 = seg.Yi;
        var xC = seg.Xi;  var yC = seg.Yi; // first ctrl approximated to start (no field for it)
        var xM = seg.Xm;  var yM = seg.Ym;
        var x1 = seg.Xj;  var y1 = seg.Yj;

        var prevX = (int)x0;
        var prevY = (int)y0;

        for (var i = 1; i <= SamplesPerSegment; i++)
        {
            var t = i / (float)SamplesPerSegment;
            var u = 1 - t;
            var b0 = u * u * u;
            var b1 = 3 * u * u * t;
            var b2 = 3 * u * t * t;
            var b3 = t * t * t;

            var px = (int)(b0 * x0 + b1 * xC + b2 * xM + b3 * x1);
            var py = (int)(b0 * y0 + b1 * yC + b2 * yM + b3 * y1);

            context.Surface.DrawLine(prevX, prevY, px, py, color);
            prevX = px;
            prevY = py;
        }
    }
}
