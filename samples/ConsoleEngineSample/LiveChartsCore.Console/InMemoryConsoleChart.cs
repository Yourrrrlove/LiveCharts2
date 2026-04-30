// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using LiveChartsCore.Console.Drawing;
using LiveChartsCore.Drawing;
using LiveChartsCore.Motion;

namespace LiveChartsCore.Console;

/// <summary>
/// Off-screen chart base that owns a <see cref="CoreMotionCanvas"/> and produces an ANSI string
/// (or writes directly to <see cref="System.Console"/>) on demand. This mirrors the role of
/// <c>InMemorySkiaSharpChart</c> in the SkiaSharp engine.
/// </summary>
public abstract class InMemoryConsoleChart
{
    static InMemoryConsoleChart()
    {
        _ = LiveChartsConsole.EnsureInitialized();
    }

    public CoreMotionCanvas CoreCanvas { get; } = new();

    /// <summary>
    /// Width in sub-pixels (= number of cell columns).
    /// </summary>
    public int Width { get; set; } = 120;

    /// <summary>
    /// Height in sub-pixels — must be even. The cell row count is Height / 2.
    /// </summary>
    public int Height { get; set; } = 40;

    public LvcColor Background { get; set; } = new(0, 0, 0);

    protected abstract Chart GetCoreChart();

    /// <summary>
    /// Renders the chart and returns it as an ANSI-encoded string. Pass <c>home=true</c> if you
    /// want to overwrite a previous frame in place at the top of the terminal.
    /// </summary>
    public string Render(bool home = false)
    {
        var coreChart = GetCoreChart()
            ?? throw new InvalidOperationException("CoreChart is not available.");

        var surface = new ConsoleSurface(Width, Height) { Background = Background };

        coreChart.Canvas.DisableAnimations = true;
        coreChart.IsLoaded = true;
        // _isFirstDraw is internal; rely on default false → Measure() sets it.

        coreChart.Measure();

        coreChart.Canvas.DrawFrame(new ConsoleDrawingContext(CoreCanvas, surface, Background));

        coreChart.Unload();

        return surface.ToAnsi(home);
    }

    /// <summary>
    /// Convenience: renders and writes to stdout.
    /// </summary>
    public void Print(bool home = false) =>
        System.Console.Out.Write(Render(home));
}
