// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using System.Text;
using LiveChartsCore.Drawing;

namespace LiveChartsCore.Console.Drawing;

/// <summary>
/// A character-grid backbuffer that exposes a pixel coordinate system. The relationship
/// between pixels and terminal cells depends on <see cref="Mode"/>:
///   * <see cref="ConsoleRenderMode.HalfBlock"/>: 1 cell = 1 col × 2 rows of sub-pixels (▀ ▄ █),
///     two colors per cell. Storage is per-cell.
///   * <see cref="ConsoleRenderMode.Braille"/>: 1 cell = 2 cols × 4 rows of sub-pixels, encoded
///     as Braille codepoints. One color per cell. Storage is per-pixel.
///   * <see cref="ConsoleRenderMode.Sixel"/>: 1 cell ≈ <see cref="CellWidth"/> × <see cref="CellHeight"/>
///     real pixels (defaults to 8×16). Output is a DCS Sixel block plus cell-positioned text
///     overlays for axis labels. Storage is per-pixel.
/// Text labels override the derived block/Braille glyph for whatever cell they land in (or
/// land on top of the Sixel image as overlay text in Sixel mode).
/// </summary>
public sealed class ConsoleSurface
{
    private struct HalfBlockCell
    {
        public LvcColor Top;     // top sub-pixel color, A=0 means transparent.
        public LvcColor Bottom;  // bottom sub-pixel color, A=0 means transparent.
    }

    private struct GlyphCell
    {
        public char Glyph;       // '\0' = no override.
        public LvcColor Fg;
        public LvcColor Bg;      // A=0 means inherit Background.
    }

    private readonly HalfBlockCell[,]? _hb;
    private readonly LvcColor[,]? _pixels;
    private readonly GlyphCell[,] _glyphs;

    public ConsoleRenderMode Mode { get; }
    public int Width { get; }
    public int Height { get; }
    public int CellWidth { get; }
    public int CellHeight { get; }
    public int CellCols => Width / CellWidth;
    public int CellRows => Height / CellHeight;

    /// <summary>
    /// Background color used when no pixel is set.
    /// </summary>
    public LvcColor Background { get; set; } = new(0, 0, 0);

    public ConsoleSurface(int width, int height, ConsoleRenderMode mode = ConsoleRenderMode.HalfBlock,
        int sixelCellWidth = 8, int sixelCellHeight = 16)
    {
        Mode = mode;
        (CellWidth, CellHeight) = mode switch
        {
            ConsoleRenderMode.Braille => (2, 4),
            ConsoleRenderMode.Sixel => (sixelCellWidth, sixelCellHeight),
            _ => (1, 2),
        };

        // Round dimensions down to a multiple of cell size so we never have a partial cell.
        Width = Math.Max(CellWidth, (width / CellWidth) * CellWidth);
        Height = Math.Max(CellHeight, (height / CellHeight) * CellHeight);

        if (mode == ConsoleRenderMode.HalfBlock)
            _hb = new HalfBlockCell[CellRows, CellCols];
        else
            _pixels = new LvcColor[Height, Width];

        _glyphs = new GlyphCell[CellRows, CellCols];
    }

    public void Clear()
    {
        // Array.Clear hits memset under the hood — much faster than the obvious 2D for-loops
        // when the surface is large (Sixel mode at 1200×660 is ~800k pixels per frame).
        if (_hb is not null) Array.Clear(_hb, 0, _hb.Length);
        if (_pixels is not null) Array.Clear(_pixels, 0, _pixels.Length);
        Array.Clear(_glyphs, 0, _glyphs.Length);
    }

    public void SetPixel(int x, int y, LvcColor color)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;

        if (_pixels is not null)
        {
            _pixels[y, x] = color;
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
            if (_hb is not null)
            {
                _hb[cellRow, col] = default;
            }
            else if (_pixels is not null)
            {
                var px0 = col * CellWidth;
                var py0 = cellRow * CellHeight;
                for (var py = 0; py < CellHeight; py++)
                    for (var px = 0; px < CellWidth; px++)
                        _pixels[py0 + py, px0 + px] = default;
            }
            col++;
        }
    }

    /// <summary>
    /// Encodes the surface into an ANSI string. Includes a leading cursor-home (\x1b[H) when
    /// <paramref name="home"/> is true so successive frames overwrite in place.
    /// </summary>
    public string ToAnsi(bool home = true)
    {
        return Mode switch
        {
            ConsoleRenderMode.Sixel => EncodeSixel(home),
            _ => EncodeCells(home),
        };
    }

    private string EncodeCells(bool home)
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

    private string EncodeSixel(bool home)
    {
        var sb = new StringBuilder(Width * Height / 3);

        // Reset SGR up-front so anything the terminal paints around the image (cells made
        // hybrid by a non-aligned cell height, rows added by Sixel-induced scroll) inherits
        // the terminal's *default* background, not whatever SGR happened to be active when
        // we got here (e.g. PSReadLine's prompt color).
        if (home) _ = sb.Append("\x1b[0m\x1b[H");

        // Labels are rasterized into the pixel grid by LabelGeometry, so this is a single
        // self-contained DCS block — no cell-text overlay pass needed.
        _ = sb.Append(SixelEncoder.Encode(_pixels!, Background));

        // Reset SGR but DON'T erase below or park the cursor: after the DCS block the
        // terminal places the cursor inside the cell row that contains the image's bottom
        // edge (a partial cell), and any \x1b[J or absolute-row positioning issued from
        // there clobbers that row — which usually contains the X-axis labels. Cell-size
        // alignment (via terminal cell-pixel-size detection in InMemoryConsoleChart) is the
        // right way to avoid the strip-below-image artifact; per-frame erasure isn't.
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

        if (_pixels is not null)
        {
            // Braille path — sample 2×4 dots from the pixel grid into a Braille codepoint.
            var bits = 0;
            LvcColor pickedColor = default;
            var baseY = r * CellHeight;
            var baseX = c * CellWidth;
            for (var dy = 0; dy < CellHeight; dy++)
            {
                for (var dx = 0; dx < CellWidth; dx++)
                {
                    var p = _pixels[baseY + dy, baseX + dx];
                    if (p.A == 0) continue;
                    var bit = dx == 0
                        ? (dy < 3 ? dy : 6)
                        : (dy < 3 ? 3 + dy : 7);
                    bits |= 1 << bit;
                    pickedColor = p; // last write wins
                }
            }
            if (bits == 0) { glyph = ' '; fg = Background; bg = Background; }
            else { glyph = (char)(0x2800 | bits); fg = pickedColor; bg = Background; }
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
        c.A == 0
            ? (foreground ? "\x1b[39m" : "\x1b[49m")  // default fg/bg → terminal shows through
            : $"\x1b[{(foreground ? 38 : 48)};2;{c.R};{c.G};{c.B}m";

    private static bool Equal(LvcColor a, LvcColor b) =>
        a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;
}
