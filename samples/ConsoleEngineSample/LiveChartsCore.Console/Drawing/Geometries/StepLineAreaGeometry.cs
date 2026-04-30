// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using LiveChartsCore.Drawing;
using LiveChartsCore.Painting;

namespace LiveChartsCore.Console.Drawing.Geometries;

/// <summary>
/// Stroke-only step-line path. The chart engine emits Segments (no cubic control points);
/// each segment becomes an L-shape: horizontal from the previous segment's end to (Xj, prev.Yj),
/// then vertical to (Xj, Yj). Mirrors SkiaSharp's StepLineAreaGeometry behavior. Fill is not
/// implemented — same scanline-filler gap as <see cref="VectorAreaGeometry"/>.
/// </summary>
public class StepLineAreaGeometry : BaseVectorGeometry, IDrawnElement<ConsoleDrawingContext>
{
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

            if (first)
            {
                // Implicit MoveTo to the segment's end (matches SkiaSharp's OnOpen).
                currentX = segment.Xj;
                currentY = segment.Yj;
                first = false;
                continue;
            }

            // Step pattern: horizontal first, then vertical.
            var hX = segment.Xj;
            var hY = currentY;
            context.Surface.DrawLine((int)currentX, (int)currentY, (int)hX, (int)hY, color);
            context.Surface.DrawLine((int)hX, (int)hY, (int)segment.Xj, (int)segment.Yj, color);

            currentX = segment.Xj;
            currentY = segment.Yj;
        }
    }
}
