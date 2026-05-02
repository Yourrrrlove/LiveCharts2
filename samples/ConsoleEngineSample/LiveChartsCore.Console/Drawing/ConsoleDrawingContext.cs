// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using LiveChartsCore.Drawing;
using LiveChartsCore.Motion;
using LiveChartsCore.Painting;

namespace LiveChartsCore.Console.Drawing;

public class ConsoleDrawingContext(CoreMotionCanvas motionCanvas, ConsoleSurface surface, LvcColor background)
    : DrawingContext
{
    public CoreMotionCanvas MotionCanvas { get; } = motionCanvas;
    public ConsoleSurface Surface { get; } = surface;
    public LvcColor Background { get; } = background;
    public LvcColor ActiveColor { get; set; } = new(255, 255, 255);

    public override void LogOnCanvas(string log)
    {
        // Log overlays are not useful in a one-shot console render; ignore.
    }

    internal override void OnBeginDraw()
    {
        Surface.Background = Background;
        Surface.Clear();
    }

    internal override void OnEndDraw() { }

    internal override void OnBeginZone(CanvasZone zone)
    {
        // Each zone carries a Clip rect. CanvasZone.NoClip uses LvcRectangle.Empty
        // (Width = Height = 0) which means "no clip"; CanvasZone.DrawMargin / XCrosshair /
        // YCrosshair carry the chart's draw-margin sub-rectangles. Translate the rect into a
        // surface-level clip so out-of-margin pixels (axis tail-end labels, series points
        // beyond the plot area) are rejected at SetPixel and don't bleed past the axes.
        var clip = zone.Clip;
        if (clip.Width <= 0 || clip.Height <= 0)
        {
            Surface.ResetClip();
        }
        else
        {
            Surface.SetClip((int)clip.X, (int)clip.Y, (int)clip.Width, (int)clip.Height);
        }
    }

    internal override void OnEndZone(CanvasZone zone) =>
        // Reset on every end so anything that draws between zones (or after the last zone)
        // inherits the full-surface clip rather than carrying a stale one.
        Surface.ResetClip();

    internal override void Draw(IDrawnElement drawable)
    {
        var opacity = ActiveOpacity;
        var element = (IDrawnElement<ConsoleDrawingContext>)drawable;

        if (ActiveLvcPaint is null)
        {
            if (element.Fill is not null) DrawByPaint(element.Fill, element, opacity);
            if (element.Stroke is not null) DrawByPaint(element.Stroke, element, opacity);
            if (element.Paint is not null) DrawByPaint(element.Paint, element, opacity);
        }
        else
        {
            if (ActiveLvcPaint.PaintStyle.HasFlag(PaintStyle.Fill))
            {
                if (element.Fill is null) element.Draw(this);
                else DrawByPaint(element.Fill, element, opacity);
            }
            if (ActiveLvcPaint.PaintStyle.HasFlag(PaintStyle.Stroke))
            {
                if (element.Stroke is null) element.Draw(this);
                else DrawByPaint(element.Stroke, element, opacity);
            }
        }
    }

    internal override void SelectPaint(Paint paint)
    {
        ActiveLvcPaint = paint;
        PaintMotionProperty.s_activePaint = paint;
        paint.OnPaintStarted(this, null);
    }

    internal override void ClearPaintSelection(Paint paint)
    {
        paint.OnPaintFinished(this, null);
        ActiveLvcPaint = null!;
        PaintMotionProperty.s_activePaint = null!;
    }

    private void DrawByPaint(Paint paint, IDrawnElement<ConsoleDrawingContext> element, float opacity)
    {
        var prevColor = ActiveColor;
        var prevPaint = ActiveLvcPaint;

        if (paint != MeasureTask.Instance)
        {
            ActiveLvcPaint = paint;
            paint.OnPaintStarted(this, element);
        }

        element.Draw(this);

        paint.OnPaintFinished(this, element);

        ActiveColor = prevColor;
        ActiveLvcPaint = prevPaint;
    }
}
