// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using LiveChartsCore.Console;
using LiveChartsCore.Console.Drawing;
using LiveChartsCore.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConsoleTests.Surface;

[TestClass]
public class ConsoleSurfaceTests
{
    [TestMethod]
    public void HalfBlock_RoundsDimensions_ToCellMultiples()
    {
        var s = new ConsoleSurface(7, 5, ConsoleRenderMode.HalfBlock);

        // HalfBlock cell = 1×2. Width 7 → 7 cells = 7px wide. Height 5 → 5/2*2 = 4px tall.
        Assert.AreEqual(7, s.Width);
        Assert.AreEqual(4, s.Height);
        Assert.AreEqual(7, s.CellCols);
        Assert.AreEqual(2, s.CellRows);
    }

    [TestMethod]
    public void Braille_RoundsDimensions_ToCellMultiples()
    {
        var s = new ConsoleSurface(11, 9, ConsoleRenderMode.Braille);

        // Braille cell = 2×4. 11/2*2 = 10. 9/4*4 = 8.
        Assert.AreEqual(10, s.Width);
        Assert.AreEqual(8, s.Height);
        Assert.AreEqual(5, s.CellCols);
        Assert.AreEqual(2, s.CellRows);
    }

    [TestMethod]
    public void Sixel_HonorsCustomCellSize()
    {
        var s = new ConsoleSurface(100, 100, ConsoleRenderMode.Sixel, sixelCellWidth: 10, sixelCellHeight: 20);

        Assert.AreEqual(10, s.CellWidth);
        Assert.AreEqual(20, s.CellHeight);
        Assert.AreEqual(100, s.Width);
        Assert.AreEqual(100, s.Height);
    }

    [TestMethod]
    public void SetClip_DropsWritesOutsideRect()
    {
        var s = new ConsoleSurface(20, 20, ConsoleRenderMode.Braille);
        s.SetClip(4, 4, 8, 8); // covers x∈[4..12), y∈[4..12)

        var red = new LvcColor(255, 0, 0);
        s.SetPixel(2, 6, red);   // outside (x < 4) → dropped
        s.SetPixel(6, 2, red);   // outside (y < 4) → dropped
        s.SetPixel(11, 11, red); // inside → kept
        s.SetPixel(12, 12, red); // outside (x >= 12) → dropped

        // ResetClip must restore full surface bounds — write at (0,0) should land afterwards.
        s.ResetClip();
        s.SetPixel(0, 0, red);

        var ansi = s.ToAnsi(home: false);

        // Two lit pixels: one inside the clipped region, one after reset. Braille codepoints
        // start at U+2800; their presence (vs ' ') is what we're asserting.
        var brailleCount = ansi.Count(c => c >= '⠀' && c <= '⣿');
        Assert.AreEqual(2, brailleCount, "expected exactly 2 lit Braille cells");
    }

    [TestMethod]
    public void SetPixel_BlendsTranslucent_AgainstBackground()
    {
        // Pure-red @ alpha=128 over a black background should land ~half-red. The blend is
        // (bg * (1-a) + color * a), with no per-pixel accumulation: a single SetPixel = a
        // single composite against Background, regardless of what was previously drawn.
        var s = new ConsoleSurface(2, 4, ConsoleRenderMode.Braille);
        s.Background = new LvcColor(0, 0, 0);

        var translucentRed = new LvcColor(255, 0, 0, 128);
        s.SetPixel(0, 0, translucentRed);

        var ansi = s.ToAnsi(home: false);

        // Expected R component: 0 * (127/255) + 255 * (128/255) ≈ 128. Sample-blended 24-bit
        // SGR escape should encode roughly 38;2;128;0;0 — allow ±2 for byte-rounding wobble.
        Assert.IsTrue(ansi.Contains("38;2;12") || ansi.Contains("38;2;13"),
            $"expected blended red ~128, got: {ansi.Replace("\x1b", "ESC")}");
    }

    [TestMethod]
    public void HalfBlock_DistinctTopBottom_EmitsUpperHalfWithFgAndBg()
    {
        var s = new ConsoleSurface(2, 2, ConsoleRenderMode.HalfBlock);
        s.Background = new LvcColor(0, 0, 0);

        s.SetPixel(0, 0, new LvcColor(255, 0, 0));   // top sub-pixel of cell (0,0) → red
        s.SetPixel(0, 1, new LvcColor(0, 255, 0));   // bottom sub-pixel → green

        var ansi = s.ToAnsi(home: false);

        // Half-block convention (see ResolveCell): both sub-pixels filled with different colors
        // → emit '▀' with top color as fg and bottom color as bg.
        Assert.IsTrue(ansi.Contains('▀'), "expected upper-half-block glyph");
        Assert.IsTrue(ansi.Contains("38;2;255;0;0"), "expected red foreground SGR");
        Assert.IsTrue(ansi.Contains("48;2;0;255;0"), "expected green background SGR");
    }

    [TestMethod]
    public void DrawText_OverridesPixelsInTargetCell()
    {
        var s = new ConsoleSurface(20, 8, ConsoleRenderMode.Braille);
        s.Background = new LvcColor(0, 0, 0);

        var red = new LvcColor(255, 0, 0);
        // Fill the cell that text will land on with red pixels first — text must wipe them.
        for (var x = 0; x < 2; x++)
            for (var y = 0; y < 4; y++)
                s.SetPixel(x, y, red);

        s.DrawText(0, 0, "X", new LvcColor(255, 255, 255));

        var ansi = s.ToAnsi(home: false);
        Assert.IsTrue(ansi.Contains('X'), "expected text glyph in output");
        // The Braille codepoint for the previously-set pixels must NOT appear at row 0,
        // because DrawText cleared the underlying cell.
        Assert.IsFalse(ansi.StartsWith('⠀'), "expected text to override leading Braille glyph");
    }

    [TestMethod]
    public void ToAnsi_WithHome_PrependsCursorReset()
    {
        var s = new ConsoleSurface(2, 2, ConsoleRenderMode.HalfBlock);
        var ansi = s.ToAnsi(home: true);
        Assert.IsTrue(ansi.StartsWith("\x1b[H"), "expected ESC[H prefix when home=true");
    }
}
