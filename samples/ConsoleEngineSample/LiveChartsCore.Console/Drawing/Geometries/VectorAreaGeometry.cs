// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using LiveChartsCore.Drawing;
using LiveChartsCore.Drawing.Segments;
using LiveChartsCore.Painting;

namespace LiveChartsCore.Console.Drawing.Geometries;

/// <summary>
/// Stroke-only line through the chart data points. Each cubic segment is sampled into a polyline
/// and rasterized via Bresenham. Fill (closing-to-pivot area) is intentionally not implemented —
/// it would need a scanline filler we don't have, and is a poor fit for a single-cell-color grid.
///
/// Mirrors SkiaSharp's CubicBezierAreaGeometry semantics: the path's "current point" carries
/// across segments, the first segment opens with an implicit MoveTo(Xi, Yi), and within each
/// segment Skia's <c>CubicTo(Xi, Yi, Xm, Ym, Xj, Yj)</c> means (Xi, Yi) is the FIRST control
/// point and (Xj, Yj) is the endpoint — the start is the previous segment's end. Treating
/// (Xi, Yi) as the start (which an earlier draft did) collapsed the first control onto the
/// start and produced curves that hugged each starting point then snapped to the end.
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

        var first = true;
        float currentX = 0, currentY = 0;

        foreach (var segment in Commands)
        {
            segment.IsValid = true;

            float startX, startY;
            if (first)
            {
                // Implicit MoveTo. The first cubic degenerates to a quadratic-feel curve since
                // start == control1, matching Skia's behavior on the opening segment.
                startX = segment.Xi;
                startY = segment.Yi;
            }
            else
            {
                startX = currentX;
                startY = currentY;
            }

            if (segment is CubicBezierSegment cubic)
            {
                SampleCubic(
                    context.Surface,
                    startX, startY,
                    cubic.Xi, cubic.Yi,   // control 1
                    cubic.Xm, cubic.Ym,   // control 2
                    cubic.Xj, cubic.Yj,   // endpoint
                    color);
            }
            else
            {
                context.Surface.DrawLine(
                    (int)startX, (int)startY,
                    (int)segment.Xj, (int)segment.Yj,
                    color);
            }

            currentX = segment.Xj;
            currentY = segment.Yj;
            first = false;
        }
    }

    private static void SampleCubic(
        ConsoleSurface surface,
        float x0, float y0,
        float c1x, float c1y,
        float c2x, float c2y,
        float x1, float y1,
        LvcColor color)
    {
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

            var px = (int)(b0 * x0 + b1 * c1x + b2 * c2x + b3 * x1);
            var py = (int)(b0 * y0 + b1 * c1y + b2 * c2y + b3 * y1);

            surface.DrawLine(prevX, prevY, px, py, color);
            prevX = px;
            prevY = py;
        }
    }
}
