// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using System.Text;
using LiveChartsCore.Drawing;

namespace LiveChartsCore.Console.Drawing;

/// <summary>
/// A character-grid backbuffer that exposes a "pixel" coordinate system at twice the height of
/// the underlying terminal cells. Each cell maps to one column of two stacked pixels which the
/// flush step encodes using upper/lower half-block characters (▀ ▄ █). Text labels override the
/// derived block glyph for the cell they land in.
/// </summary>
public sealed class ConsoleSurface
{
    private struct Cell
    {
        public LvcColor Top;     // top sub-pixel color, A=0 means transparent.
        public LvcColor Bottom;  // bottom sub-pixel color, A=0 means transparent.
        public char Glyph;       // '\0' = no override; otherwise printed as-is.
        public LvcColor GlyphFg; // foreground for Glyph when set.
        public LvcColor GlyphBg; // background for Glyph when set; A=0 means inherit.
    }

    private readonly Cell[,] _cells;

    /// <summary>
    /// Width in pixels (= number of cell columns).
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Height in pixels (must be even — = 2 * number of cell rows).
    /// </summary>
    public int Height { get; }

    public int CellCols => Width;
    public int CellRows => Height / 2;

    /// <summary>
    /// Background color used when no sub-pixel is set.
    /// </summary>
    public LvcColor Background { get; set; } = new(0, 0, 0);

    public ConsoleSurface(int width, int height)
    {
        if (height % 2 != 0) height++;
        Width = width;
        Height = height;
        _cells = new Cell[CellRows, CellCols];
    }

    public void Clear()
    {
        var rows = CellRows;
        var cols = CellCols;
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
                _cells[r, c] = default;
    }

    /// <summary>
    /// Sets a single sub-pixel.
    /// </summary>
    public void SetPixel(int x, int y, LvcColor color)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;

        ref var cell = ref _cells[y >> 1, x];
        if ((y & 1) == 0) cell.Top = color;
        else cell.Bottom = color;
    }

    /// <summary>
    /// Bresenham line at sub-pixel resolution.
    /// </summary>
    public void DrawLine(int x0, int y0, int x1, int y1, LvcColor color)
    {
        var dx = Math.Abs(x1 - x0);
        var dy = -Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx + dy;

        while (true)
        {
            SetPixel(x0, y0, color);
            if (x0 == x1 && y0 == y1) break;
            var e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    /// <summary>
    /// Fills a rectangle in sub-pixel coordinates.
    /// </summary>
    public void FillRect(int x, int y, int w, int h, LvcColor color)
    {
        var x0 = Math.Max(0, x);
        var y0 = Math.Max(0, y);
        var x1 = Math.Min(Width, x + w);
        var y1 = Math.Min(Height, y + h);
        for (var py = y0; py < y1; py++)
            for (var px = x0; px < x1; px++)
                SetPixel(px, py, color);
    }

    /// <summary>
    /// Strokes a rectangle outline (one sub-pixel thick).
    /// </summary>
    public void StrokeRect(int x, int y, int w, int h, LvcColor color)
    {
        if (w <= 0 || h <= 0) return;
        DrawLine(x, y, x + w - 1, y, color);
        DrawLine(x, y + h - 1, x + w - 1, y + h - 1, color);
        DrawLine(x, y, x, y + h - 1, color);
        DrawLine(x + w - 1, y, x + w - 1, y + h - 1, color);
    }

    /// <summary>
    /// Writes text into the cell row that contains pixel-y. Each character takes one cell column.
    /// </summary>
    public void DrawText(int xPx, int yPx, string text, LvcColor fg, LvcColor bg = default)
    {
        if (string.IsNullOrEmpty(text)) return;
        var cellRow = yPx >> 1;
        if (cellRow < 0 || cellRow >= CellRows) return;

        var col = xPx;
        foreach (var ch in text)
        {
            if (col < 0) { col++; continue; }
            if (col >= CellCols) break;

            ref var cell = ref _cells[cellRow, col];
            cell.Glyph = ch;
            cell.GlyphFg = fg;
            cell.GlyphBg = bg;
            // Clear any existing block info so the glyph wins cleanly.
            cell.Top = default;
            cell.Bottom = default;
            col++;
        }
    }

    /// <summary>
    /// Encodes the surface into an ANSI string. Uses 24-bit color escapes.
    /// Includes a leading cursor-home (\x1b[H) so successive calls overwrite in place.
    /// </summary>
    public string ToAnsi(bool home = true)
    {
        var sb = new StringBuilder(CellRows * CellCols * 8);
        if (home) _ = sb.Append("\x1b[H");

        var lastFg = new LvcColor(255, 255, 255);
        var lastBg = Background;
        _ = sb.Append(Esc(lastFg, true));
        _ = sb.Append(Esc(lastBg, false));

        for (var r = 0; r < CellRows; r++)
        {
            for (var c = 0; c < CellCols; c++)
            {
                var cell = _cells[r, c];
                char glyph;
                LvcColor fg, bg;

                if (cell.Glyph != '\0')
                {
                    glyph = cell.Glyph;
                    fg = cell.GlyphFg;
                    bg = cell.GlyphBg.A == 0 ? Background : cell.GlyphBg;
                }
                else
                {
                    var hasTop = cell.Top.A != 0;
                    var hasBot = cell.Bottom.A != 0;
                    if (!hasTop && !hasBot)
                    {
                        glyph = ' ';
                        fg = Background;
                        bg = Background;
                    }
                    else if (hasTop && !hasBot)
                    {
                        glyph = '▀'; // ▀
                        fg = cell.Top;
                        bg = Background;
                    }
                    else if (!hasTop && hasBot)
                    {
                        glyph = '▄'; // ▄
                        fg = cell.Bottom;
                        bg = Background;
                    }
                    else if (Equal(cell.Top, cell.Bottom))
                    {
                        glyph = '█'; // █
                        fg = cell.Top;
                        bg = Background;
                    }
                    else
                    {
                        glyph = '▀'; // ▀ with bg
                        fg = cell.Top;
                        bg = cell.Bottom;
                    }
                }

                if (!Equal(fg, lastFg)) { _ = sb.Append(Esc(fg, true)); lastFg = fg; }
                if (!Equal(bg, lastBg)) { _ = sb.Append(Esc(bg, false)); lastBg = bg; }
                _ = sb.Append(glyph);
            }
            if (r < CellRows - 1) _ = sb.Append('\n');
        }

        _ = sb.Append("\x1b[0m"); // reset
        return sb.ToString();
    }

    private static string Esc(LvcColor c, bool foreground) =>
        $"\x1b[{(foreground ? 38 : 48)};2;{c.R};{c.G};{c.B}m";

    private static bool Equal(LvcColor a, LvcColor b) =>
        a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;
}
