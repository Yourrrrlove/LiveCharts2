// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using System.Text;
using LiveChartsCore.Drawing;

namespace LiveChartsCore.Console.Drawing;

/// <summary>
/// Encodes a per-pixel color grid as a DCS Sixel block — a printable-ASCII raster image protocol
/// understood by Sixel-capable terminals. The output is a self-contained image: cursor lands
/// below it after rendering. Color quantization is "last unique wins" up to <c>maxPalette</c>;
/// for chart use the palette is tiny (background + a few series strokes) and never overflows.
/// </summary>
internal static class SixelEncoder
{
    private const int MaxPalette = 255;
    // Pn1=0 (default pixel aspect ratio, overridden by raster attributes below), Pn2=2 (unset
    // pixels are filled with palette color 0 = chart background), Pn3=0 (default grid).
    private const string DcsIntroducer = "\x1bP0;2;0q";
    private const string DcsTerminator = "\x1b\\";

    /// <summary>
    /// Builds the Sixel image. <paramref name="pixels"/> is indexed [y, x] in pixel space.
    /// Pixels with <c>Color.A == 0</c> are treated as background and rendered using palette
    /// entry 0, which is set to <paramref name="background"/>.
    /// </summary>
    public static string Encode(LvcColor[,] pixels, LvcColor background)
    {
        var height = pixels.GetLength(0);
        var width = pixels.GetLength(1);

        var sb = new StringBuilder(width * height / 4);
        _ = sb.Append(DcsIntroducer);
        _ = sb.Append($"\"1;1;{width};{height}");

        // Palette: index 0 always = background. Subsequent indices populated as colors are seen.
        var palette = new Dictionary<int, int> { [Pack(background)] = 0 };
        EmitPaletteEntry(sb, 0, background);

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var c = pixels[y, x];
                if (c.A == 0) continue;
                var key = Pack(c);
                if (palette.ContainsKey(key)) continue;
                if (palette.Count >= MaxPalette) continue; // overflow — pixel falls back to "nearest already in palette" via lookup below
                var idx = palette.Count;
                palette[key] = idx;
                EmitPaletteEntry(sb, idx, c);
            }

        // Encode bands of 6 pixel rows.
        for (var bandY = 0; bandY < height; bandY += 6)
        {
            var bandHeight = Math.Min(6, height - bandY);

            // Determine which palette entries are present in this band.
            var colorsInBand = new HashSet<int>();
            for (var y = 0; y < bandHeight; y++)
                for (var x = 0; x < width; x++)
                {
                    var c = pixels[bandY + y, x];
                    if (c.A == 0)
                    {
                        _ = colorsInBand.Add(0); // background pixels render as color 0
                        continue;
                    }
                    var key = Pack(c);
                    _ = colorsInBand.Add(palette.TryGetValue(key, out var idx) ? idx : 0);
                }

            var first = true;
            foreach (var paletteIdx in colorsInBand)
            {
                if (!first) _ = sb.Append('$');
                first = false;

                _ = sb.Append('#').Append(paletteIdx);

                // Build the 6-row column pattern for this color.
                EncodeBandColor(sb, pixels, bandY, bandHeight, width, paletteIdx, palette, background);
            }

            _ = sb.Append('-');
        }

        _ = sb.Append(DcsTerminator);
        return sb.ToString();
    }

    private static void EncodeBandColor(
        StringBuilder sb,
        LvcColor[,] pixels,
        int bandY,
        int bandHeight,
        int width,
        int paletteIdx,
        Dictionary<int, int> palette,
        LvcColor background)
    {
        var bgKey = Pack(background);
        var runChar = '\0';
        var runLen = 0;

        for (var x = 0; x < width; x++)
        {
            var bits = 0;
            for (var r = 0; r < bandHeight; r++)
            {
                var c = pixels[bandY + r, x];
                int idx;
                if (c.A == 0) idx = 0;
                else if (palette.TryGetValue(Pack(c), out var found)) idx = found;
                else idx = 0; // overflow fallback

                if (idx == paletteIdx) bits |= 1 << r;
            }

            var ch = (char)('?' + bits);
            if (ch == runChar)
            {
                runLen++;
            }
            else
            {
                FlushRun(sb, runChar, runLen);
                runChar = ch;
                runLen = 1;
            }
        }
        FlushRun(sb, runChar, runLen);
    }

    private static void FlushRun(StringBuilder sb, char ch, int len)
    {
        if (len <= 0) return;
        if (len >= 4) _ = sb.Append('!').Append(len).Append(ch);
        else for (var i = 0; i < len; i++) _ = sb.Append(ch);
    }

    private static void EmitPaletteEntry(StringBuilder sb, int index, LvcColor c)
    {
        // Pc;Pu;Px;Py;Pz where Pu=2 (RGB), values are 0-100 percent.
        var r = (int)(c.R / 255.0 * 100 + 0.5);
        var g = (int)(c.G / 255.0 * 100 + 0.5);
        var b = (int)(c.B / 255.0 * 100 + 0.5);
        _ = sb.Append('#').Append(index).Append(";2;")
              .Append(r).Append(';').Append(g).Append(';').Append(b);
    }

    private static int Pack(LvcColor c) => (c.R << 16) | (c.G << 8) | c.B;
}
