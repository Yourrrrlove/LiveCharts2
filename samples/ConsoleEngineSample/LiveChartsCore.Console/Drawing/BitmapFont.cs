// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using LiveChartsCore.Drawing;

namespace LiveChartsCore.Console.Drawing;

/// <summary>
/// Tiny 5×7 monospace bitmap font used to rasterize labels into the pixel grid in
/// <see cref="ConsoleRenderMode.Sixel"/> mode. Cell-grid modes (HalfBlock, Braille) render
/// labels as ANSI text glyphs and don't need this. Glyph coverage is the minimum a numeric
/// chart axis needs — digits, decimal point, sign, scientific notation. Unknown characters
/// are silently skipped.
/// </summary>
internal static class BitmapFont
{
    public const int GlyphWidth = 5;
    public const int GlyphHeight = 7;
    public const int GlyphSpacing = 1;

    // Each pattern is 7 rows; per row, the low 5 bits are columns (bit 4 = leftmost col).
    private static readonly Dictionary<char, byte[]> Glyphs = new()
    {
        ['0'] = [0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110],
        ['1'] = [0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110],
        ['2'] = [0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b01000, 0b11111],
        ['3'] = [0b01110, 0b10001, 0b00001, 0b00110, 0b00001, 0b10001, 0b01110],
        ['4'] = [0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010],
        ['5'] = [0b11111, 0b10000, 0b11110, 0b00001, 0b00001, 0b10001, 0b01110],
        ['6'] = [0b00110, 0b01000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110],
        ['7'] = [0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000],
        ['8'] = [0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110],
        ['9'] = [0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00010, 0b01100],
        ['-'] = [0, 0, 0, 0b11111, 0, 0, 0],
        ['.'] = [0, 0, 0, 0, 0, 0b01100, 0b01100],
        [','] = [0, 0, 0, 0, 0b01100, 0b01100, 0b01000],
        ['+'] = [0, 0b00100, 0b00100, 0b11111, 0b00100, 0b00100, 0],
        ['e'] = [0, 0, 0b01110, 0b10011, 0b11110, 0b10000, 0b01110],
        ['E'] = [0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111],
        [':'] = [0, 0b01100, 0b01100, 0, 0b01100, 0b01100, 0],
        ['/'] = [0b00001, 0b00010, 0b00010, 0b00100, 0b01000, 0b01000, 0b10000],
        [' '] = [0, 0, 0, 0, 0, 0, 0],
    };

    /// <summary>
    /// Rasterizes a single glyph into the surface pixel grid. Unknown chars are skipped without
    /// advancing — caller manages the X cursor.
    /// </summary>
    public static void DrawChar(ConsoleSurface surface, int x, int y, char ch, LvcColor color, int scale = 2)
    {
        if (!Glyphs.TryGetValue(ch, out var pattern)) return;

        for (var row = 0; row < GlyphHeight; row++)
        {
            var bits = pattern[row];
            if (bits == 0) continue;

            for (var col = 0; col < GlyphWidth; col++)
            {
                if (((bits >> (GlyphWidth - 1 - col)) & 1) == 0) continue;
                for (var dy = 0; dy < scale; dy++)
                    for (var dx = 0; dx < scale; dx++)
                        surface.SetPixel(x + col * scale + dx, y + row * scale + dy, color);
            }
        }
    }

    /// <summary>
    /// Rasterizes a string starting at (<paramref name="x"/>, <paramref name="y"/>). Step width
    /// includes inter-glyph spacing so consecutive chars don't touch.
    /// </summary>
    public static void DrawText(ConsoleSurface surface, int x, int y, string text, LvcColor color, int scale = 2)
    {
        if (string.IsNullOrEmpty(text)) return;
        var step = (GlyphWidth + GlyphSpacing) * scale;
        foreach (var ch in text)
        {
            DrawChar(surface, x, y, ch, color, scale);
            x += step;
        }
    }

    public static int CellWidth(int scale = 2) => (GlyphWidth + GlyphSpacing) * scale;
    public static int CellHeight(int scale = 2) => GlyphHeight * scale;
}
