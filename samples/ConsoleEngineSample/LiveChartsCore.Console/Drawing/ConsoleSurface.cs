// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using System.Text;
using LiveChartsCore.Drawing;

namespace LiveChartsCore.Console.Drawing;

/// <summary>
/// A character-grid backbuffer that exposes a sub-pixel coordinate system. The relationship
/// between sub-pixels and terminal cells depends on <see cref="Mode"/>:
///   * <see cref="ConsoleRenderMode.HalfBlock"/>: 1 cell = 1 col × 2 rows of pixels (▀ ▄ █),
///     two colors per cell.
///   * <see cref="ConsoleRenderMode.Braille"/>: 1 cell = 2 cols × 4 rows of pixel dots,
///     one color per cell. Quadruples the resolution.
/// Text labels override the derived block/Braille glyph for whatever cell they land in.
/// </summary>
public sealed class ConsoleSurface
{
    private struct HalfBlockCell
    {
        public LvcColor Top;     // top sub-pixel color, A=0 means transparent.
        public LvcColor Bottom;  // bottom sub-pixel color, A=0 means transparent.
    }

    private struct BrailleCell
    {
        public byte Dots;        // bitmap of the 8 Braille dots — see Set/EncodeBraille.
        public LvcColor Color;   // last color written to any dot in this cell.
    }

    private struct GlyphCell
    {
        public char Glyph;       // '\0' = no override.
        public LvcColor Fg;
        public LvcColor Bg;      // A=0 means inherit Background.
    }

    private readonly HalfBlockCell[,]? _hb;
    private readonly BrailleCell[,]? _br;
    private readonly GlyphCell[,] _glyphs;

    public ConsoleRenderMode Mode { get; }
    public int Width { get; }
    public int Height { get; }
    public int CellWidth => Mode == ConsoleRenderMode.Braille ? 2 : 1;
    public int CellHeight => Mode == ConsoleRenderMode.Braille ? 4 : 2;
    public int CellCols => Width / CellWidth;
    public int CellRows => Height / CellHeight;

    /// <summary>
    /// Background color used when no sub-pixel is set.
    /// </summary>
    public LvcColor Background { get; set; } = new(0, 0, 0);

    public ConsoleSurface(int width, int height, ConsoleRenderMode mode = ConsoleRenderMode.HalfBlock)
    {
        Mode = mode;
        var cw = mode == ConsoleRenderMode.Braille ? 2 : 1;
        var ch = mode == ConsoleRenderMode.Braille ? 4 : 2;

        // Round dimensions down to a multiple of the cell size so we never have a partial cell.
        Width = Math.Max(cw, (width / cw) * cw);
        Height = Math.Max(ch, (height / ch) * ch);

        if (mode == ConsoleRenderMode.Braille)
            _br = new BrailleCell[CellRows, CellCols];
        else
            _hb = new HalfBlockCell[CellRows, CellCols];

        _glyphs = new GlyphCell[CellRows, CellCols];
    }

    public void Clear()
    {
        var rows = CellRows;
        var cols = CellCols;
        if (_hb is not null)
            for (var r = 0; r < rows; r++)
                for (var c = 0; c < cols; c++) _hb[r, c] = default;
        if (_br is not null)
            for (var r = 0; r < rows; r++)
                for (var c = 0; c < cols; c++) _br[r, c] = default;
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++) _glyphs[r, c] = default;
    }

    public void SetPixel(int x, int y, LvcColor color)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;

        if (_br is not null)
        {
            var cellCol = x >> 1;
            var cellRow = y >> 2;
            var dx = x & 1;
            var dy = y & 3;
            // Braille bit positions:
            //   left col (dx=0):  rows 0,1,2 → bits 0,1,2 (dots 1,2,3); row 3 → bit 6 (dot 7)
            //   right col (dx=1): rows 0,1,2 → bits 3,4,5 (dots 4,5,6); row 3 → bit 7 (dot 8)
            var bit = dx == 0
                ? (dy < 3 ? dy : 6)
                : (dy < 3 ? 3 + dy : 7);

            ref var cell = ref _br[cellRow, cellCol];
            cell.Dots |= (byte)(1 << bit);
            cell.Color = color;
            return;
        }

        ref var hb = ref _hb![y >> 1, x];
        if ((y & 1) == 0) hb.Top = color;
        else hb.Bottom = color;
    }

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

    public void StrokeRect(int x, int y, int w, int h, LvcColor color)
    {
        if (w <= 0 || h <= 0) return;
        DrawLine(x, y, x + w - 1, y, color);
        DrawLine(x, y + h - 1, x + w - 1, y + h - 1, color);
        DrawLine(x, y, x, y + h - 1, color);
        DrawLine(x + w - 1, y, x + w - 1, y + h - 1, color);
    }

    /// <summary>
    /// Writes text into the cell row that contains pixel-y. Each character takes one cell.
    /// </summary>
    public void DrawText(int xPx, int yPx, string text, LvcColor fg, LvcColor bg = default)
    {
        if (string.IsNullOrEmpty(text)) return;
        var cellRow = yPx / CellHeight;
        if (cellRow < 0 || cellRow >= CellRows) return;

        var col = xPx / CellWidth;
        foreach (var ch in text)
        {
            if (col < 0) { col++; continue; }
            if (col >= CellCols) break;

            _glyphs[cellRow, col] = new GlyphCell { Glyph = ch, Fg = fg, Bg = bg };
            // Glyph supersedes any pixels in the same cell.
            if (_hb is not null) _hb[cellRow, col] = default;
            if (_br is not null) _br[cellRow, col] = default;
            col++;
        }
    }

    /// <summary>
    /// Encodes the surface into an ANSI string with 24-bit color escapes. Includes a leading
    /// cursor-home (\x1b[H) when <paramref name="home"/> is true so successive frames overwrite.
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
                ResolveCell(r, c, out var glyph, out var fg, out var bg);

                if (!Equal(fg, lastFg)) { _ = sb.Append(Esc(fg, true)); lastFg = fg; }
                if (!Equal(bg, lastBg)) { _ = sb.Append(Esc(bg, false)); lastBg = bg; }
                _ = sb.Append(glyph);
            }
            if (r < CellRows - 1) _ = sb.Append('\n');
        }

        _ = sb.Append("\x1b[0m");
        return sb.ToString();
    }

    private void ResolveCell(int r, int c, out char glyph, out LvcColor fg, out LvcColor bg)
    {
        var g = _glyphs[r, c];
        if (g.Glyph != '\0')
        {
            glyph = g.Glyph;
            fg = g.Fg;
            bg = g.Bg.A == 0 ? Background : g.Bg;
            return;
        }

        if (_br is not null)
        {
            var cell = _br[r, c];
            if (cell.Dots == 0)
            {
                glyph = ' ';
                fg = Background;
                bg = Background;
            }
            else
            {
                glyph = (char)(0x2800 | cell.Dots);
                fg = cell.Color;
                bg = Background;
            }
            return;
        }

        var hb = _hb![r, c];
        var hasTop = hb.Top.A != 0;
        var hasBot = hb.Bottom.A != 0;
        if (!hasTop && !hasBot) { glyph = ' '; fg = Background; bg = Background; }
        else if (hasTop && !hasBot) { glyph = '▀'; fg = hb.Top; bg = Background; }
        else if (!hasTop && hasBot) { glyph = '▄'; fg = hb.Bottom; bg = Background; }
        else if (Equal(hb.Top, hb.Bottom)) { glyph = '█'; fg = hb.Top; bg = Background; }
        else { glyph = '▀'; fg = hb.Top; bg = hb.Bottom; }
    }

    private static string Esc(LvcColor c, bool foreground) =>
        $"\x1b[{(foreground ? 38 : 48)};2;{c.R};{c.G};{c.B}m";

    private static bool Equal(LvcColor a, LvcColor b) =>
        a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;
}
