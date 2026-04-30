// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using System.Text;
using LiveChartsCore.Drawing;

namespace LiveChartsCore.Console.Drawing;

/// <summary>
/// Encodes a per-pixel color grid as a DCS Sixel block — a printable-ASCII raster image protocol
/// understood by Sixel-capable terminals. Output is self-contained: cursor lands below the image.
/// Hot path optimized for chart-shaped use (small palette, repeated calls): pass-1 walks the
/// pixel grid once to build the palette and a flat <c>byte[]</c> index buffer; pass-2 iterates
/// 6-row bands using bool-array color presence and RLE-compresses runs of identical sixels.
/// </summary>
internal static class SixelEncoder
{
    private const int MaxPalette = 255;
    private const byte TransparentIndex = 0xFF;

    public static string Encode(LvcColor[,] pixels, LvcColor background)
    {
        var height = pixels.GetLength(0);
        var width = pixels.GetLength(1);
        var transparent = background.A == 0;

        var sb = new StringBuilder(width * height / 6);
        // Pn1=0 (default aspect, raster attrs override), Pn2=2 opaque / 1 transparent, Pn3=0.
        _ = sb.Append(transparent ? "\x1bP0;1;0q" : "\x1bP0;2;0q");
        _ = sb.Append('"').Append("1;1;").Append(width).Append(';').Append(height);

        var palette = new Dictionary<int, byte>(16);
        var paletteSize = 0;
        if (!transparent)
        {
            palette[Pack(background)] = 0;
            EmitPaletteEntry(sb, 0, background);
            paletteSize = 1;
        }

        // Pass 1 — convert LvcColor[,] to byte[] of palette indices in row-major order.
        // This collapses the per-pixel hashtable lookup into a single sweep and gives the
        // band loop a flat buffer to iterate (much friendlier to the prefetcher than a 2D
        // array, and lets the JIT elide bounds checks in the inner loop).
        var indices = new byte[height * width];
        for (var y = 0; y < height; y++)
        {
            var rowOff = y * width;
            for (var x = 0; x < width; x++)
            {
                var c = pixels[y, x];
                byte idx;
                if (c.A == 0)
                {
                    idx = transparent ? TransparentIndex : (byte)0;
                }
                else
                {
                    var key = Pack(c);
                    if (palette.TryGetValue(key, out var found))
                    {
                        idx = found;
                    }
                    else if (paletteSize < MaxPalette)
                    {
                        idx = (byte)paletteSize;
                        palette[key] = idx;
                        EmitPaletteEntry(sb, idx, c);
                        paletteSize++;
                    }
                    else
                    {
                        idx = transparent ? TransparentIndex : (byte)0; // overflow → drop
                    }
                }
                indices[rowOff + x] = idx;
            }
        }

        // Pass 2 — per band: discover colors with a bool[256] presence array, then emit one
        // RLE-compressed pass per color.
        var present = new bool[256];
        var presentList = new List<byte>(8);

        for (var bandY = 0; bandY < height; bandY += 6)
        {
            var bandHeight = Math.Min(6, height - bandY);

            for (var i = 0; i < presentList.Count; i++) present[presentList[i]] = false;
            presentList.Clear();

            for (var y = 0; y < bandHeight; y++)
            {
                var rowOff = (bandY + y) * width;
                for (var x = 0; x < width; x++)
                {
                    var idx = indices[rowOff + x];
                    if (idx == TransparentIndex) continue;
                    if (!present[idx]) { present[idx] = true; presentList.Add(idx); }
                }
            }

            var first = true;
            foreach (var paletteIdx in presentList)
            {
                if (!first) _ = sb.Append('$');
                first = false;
                _ = sb.Append('#').Append(paletteIdx);
                EncodeBandColor(sb, indices, width, bandY, bandHeight, paletteIdx);
            }

            _ = sb.Append('-');
        }

        _ = sb.Append("\x1b\\");
        return sb.ToString();
    }

    private static void EncodeBandColor(
        StringBuilder sb,
        byte[] indices,
        int width,
        int bandY,
        int bandHeight,
        byte paletteIdx)
    {
        var runChar = '\0';
        var runLen = 0;
        var rowBase = bandY * width;

        for (var x = 0; x < width; x++)
        {
            var bits = 0;
            // Unroll the 6 row checks; bandHeight is at most 6 and almost always exactly 6.
            for (var r = 0; r < bandHeight; r++)
            {
                if (indices[rowBase + r * width + x] == paletteIdx) bits |= 1 << r;
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
        // Pc;Pu;Px;Py;Pz where Pu=2 (RGB), values 0-100 percent.
        var r = (c.R * 100 + 127) / 255;
        var g = (c.G * 100 + 127) / 255;
        var b = (c.B * 100 + 127) / 255;
        _ = sb.Append('#').Append(index).Append(";2;")
              .Append(r).Append(';').Append(g).Append(';').Append(b);
    }

    private static int Pack(LvcColor c) => (c.R << 16) | (c.G << 8) | c.B;
}
