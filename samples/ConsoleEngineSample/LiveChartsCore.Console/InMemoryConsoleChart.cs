// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using LiveChartsCore.Console.Drawing;
using LiveChartsCore.Drawing;
using LiveChartsCore.Motion;

namespace LiveChartsCore.Console;

/// <summary>
/// Off-screen chart base that owns a <see cref="CoreMotionCanvas"/> and produces an ANSI string
/// (or writes directly to <see cref="System.Console"/>) on demand. Mirrors the role of
/// <c>InMemorySkiaSharpChart</c> in the SkiaSharp engine, but adds a live-loop helper since
/// terminals can be redrawn in place via cursor-home escapes.
/// </summary>
public abstract class InMemoryConsoleChart
{
    static InMemoryConsoleChart()
    {
        _ = LiveChartsConsole.EnsureInitialized();
    }

    private ConsoleSurface? _surface;

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
    /// One-shot render. Disables animations so the result is the fully-settled state, then
    /// unloads the chart. Use for "save image" / snapshot workflows.
    /// </summary>
    public string Render(bool home = false)
    {
        var coreChart = GetCoreChart()
            ?? throw new InvalidOperationException("CoreChart is not available.");

        var surface = AcquireSurface();

        coreChart.Canvas.DisableAnimations = true;
        coreChart.IsLoaded = true;

        coreChart.Measure();
        coreChart.Canvas.DrawFrame(new ConsoleDrawingContext(CoreCanvas, surface, Background));

        coreChart.Unload();
        _surface = null; // unload disposes paint state, force a fresh surface next call.

        return surface.ToAnsi(home);
    }

    /// <summary>
    /// Draws a single live frame. Does NOT disable animations or unload the chart, so successive
    /// calls drive the motion system and produce in-flight tween states. Cheap to call in a loop.
    /// </summary>
    public string RenderFrame(bool home = true)
    {
        var coreChart = GetCoreChart()
            ?? throw new InvalidOperationException("CoreChart is not available.");

        var surface = AcquireSurface();

        // Idempotent — Load is invoked once in the constructor; this just guards re-entry.
        coreChart.IsLoaded = true;

        coreChart.Measure();
        coreChart.Canvas.DrawFrame(new ConsoleDrawingContext(CoreCanvas, surface, Background));

        return surface.ToAnsi(home);
    }

    /// <summary>
    /// Pumps <see cref="RenderFrame"/> at the requested cadence to <paramref name="output"/>
    /// (defaulting to <see cref="System.Console.Out"/>) until <paramref name="ct"/> fires.
    /// Hides the cursor while running and resets terminal state on exit.
    /// </summary>
    public async Task RenderLoopAsync(
        int fps = 30,
        TextWriter? output = null,
        CancellationToken ct = default)
    {
        output ??= System.Console.Out;
        var period = TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, fps));

        await output.WriteAsync("\x1b[?25l\x1b[2J"); // hide cursor + clear screen

        try
        {
            while (!ct.IsCancellationRequested)
            {
                AdaptToTerminalSize();

                var frame = RenderFrame(home: true);
                await output.WriteAsync(frame);
                await output.FlushAsync(ct);

                try { await Task.Delay(period, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            await output.WriteAsync("\x1b[0m\x1b[?25h\n"); // reset color, show cursor.
            await output.FlushAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Convenience: one-shot render + write to stdout.
    /// </summary>
    public void Print(bool home = false) =>
        System.Console.Out.Write(Render(home));

    private ConsoleSurface AcquireSurface()
    {
        if (_surface is null || _surface.Width != Width || _surface.Height != Height)
            _surface = new ConsoleSurface(Width, Height);
        _surface.Background = Background;
        _surface.Clear();
        return _surface;
    }

    private void AdaptToTerminalSize()
    {
        if (System.Console.IsOutputRedirected) return;

        int cols, rows;
        try
        {
            cols = Math.Max(20, System.Console.WindowWidth - 1);
            rows = Math.Max(6, System.Console.WindowHeight - 2);
        }
        catch (IOException)
        {
            return;
        }

        var newWidth = cols;
        var newHeight = rows * 2;
        if (newWidth == Width && newHeight == Height) return;

        Width = newWidth;
        Height = newHeight;
    }
}
