// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using LiveChartsCore.Drawing;

namespace LiveChartsCore.Console.Drawing.Geometries;

public class LabelGeometry : BaseLabelGeometry, IDrawnElement<ConsoleDrawingContext>
{
    public virtual void Draw(ConsoleDrawingContext context)
    {
        if (string.IsNullOrEmpty(Text)) return;

        var size = MeasuredSize();
        var dx = HorizontalAlign switch
        {
            Align.Start => 0f,
            Align.Middle => -size.Width / 2f,
            Align.End => -size.Width,
            _ => 0f
        };
        // Vertical: a label is one cell tall (= 2 sub-pixels) so anchor it cleanly.
        var dy = VerticalAlign switch
        {
            Align.Start => 0f,
            Align.Middle => -1f,
            Align.End => -2f,
            _ => 0f
        };

        context.Surface.DrawText(
            (int)(X + dx), (int)(Y + dy), Text, context.ActiveColor);
    }

    public override LvcSize Measure() => MeasuredSize();

    private LvcSize MeasuredSize()
    {
        // 1 char = 1 cell column = 1 sub-pixel wide. Height = 2 sub-pixels (one cell row).
        var w = (Text?.Length ?? 0) + Padding.Left + Padding.Right;
        var h = 2 + Padding.Top + Padding.Bottom;
        return new LvcSize(w, h);
    }
}
