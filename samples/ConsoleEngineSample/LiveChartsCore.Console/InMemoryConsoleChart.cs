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
    private ConsoleRenderMode _mode = ConsoleRenderMode.HalfBlock;

    public CoreMotionCanvas CoreCanvas { get; } = new();

    /// <summary>
    /// Width in sub-pixels (= cells × <see cref="ConsoleSurface.CellWidth"/> for the current mode).
    /// </summary>
    public int Width { get; set; } = 120;

    /// <summary>
    /// Height in sub-pixels (= cells × <see cref="ConsoleSurface.CellHeight"/> for the current mode).
    /// </summary>
    public int Height { get; set; } = 40;

    public LvcColor Background { get; set; } = new(0, 0, 0);

    /// <summary>
    /// Lock object that <see cref="Render"/> and <see cref="RenderFrame"/> hold for the duration
    /// of measurement + draw. Callers mutating chart data from a thread other than the render
    /// thread (e.g. an <c>ObservableCollection&lt;T&gt;</c> driven by a background task) MUST
    /// take this lock around their mutations to avoid mid-enumeration exceptions in the chart's
    /// data factory.
    /// </summary>
    public object SyncRoot { get; } = new();

    /// <summary>
    /// Cell encoding for the next render. Set this BEFORE first render. Switching mode in-flight
    /// is allowed but recreates the surface and resets the cached cell-size that LabelGeometry
    /// uses for layout — call <see cref="ConfigureFromTerminalCells"/> afterwards if you sized
    /// the chart by cell count.
    /// </summary>
    public ConsoleRenderMode RenderMode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            _surface = null;
            UpdateGlobalCellSize();
        }
    }

    /// <summary>
    /// Pixels per terminal cell when in <see cref="ConsoleRenderMode.Sixel"/>. Defaults are tuned
    /// for VSCode's default monospace at default zoom; bump them if your terminal uses a larger
    /// font (axis labels at the bottom of the chart will land in white space below the image
    /// when these are too small) or shrink them if labels overlap the chart contents.
    /// </summary>
    public int SixelCellWidth { get; set; } = 10;
    public int SixelCellHeight { get; set; } = 22;

    /// <summary>
    /// Sizes <see cref="Width"/> and <see cref="Height"/> so the chart fills the given number of
    /// terminal cells under the current <see cref="RenderMode"/>.
    /// </summary>
    public void ConfigureFromTerminalCells(int cellCols, int cellRows)
    {
        var (cw, ch) = CellPixelSize();
        Width = Math.Max(cw, cellCols * cw);
        Height = Math.Max(ch, cellRows * ch);
        UpdateGlobalCellSize();
    }

    private (int cw, int ch) CellPixelSize() => _mode switch
    {
        ConsoleRenderMode.Braille => (2, 4),
        ConsoleRenderMode.Sixel => (SixelCellWidth, SixelCellHeight),
        _ => (1, 2),
    };

    private void UpdateGlobalCellSize()
    {
        // Read by LabelGeometry.Measure so axis layout reserves the right number of pixels per
        // character. Single global value matches the "single mode per process" reality of these
        // in-memory charts; if you need two simultaneously, render them sequentially.
        var (w, h) = CellPixelSize();
        Drawing.Geometries.LabelGeometry.GlyphPixelsW = w;
        Drawing.Geometries.LabelGeometry.GlyphPixelsH = h;
    }

    protected abstract Chart GetCoreChart();

    /// <summary>
    /// One-shot render. Disables animations so the result is the fully-settled state, then
    /// unloads the chart. Use for "save image" / snapshot workflows.
    /// </summary>
    public string Render(bool home = false)
    {
        var coreChart = GetCoreChart()
            ?? throw new InvalidOperationException("CoreChart is not available.");

        lock (SyncRoot)
        {
            var surface = AcquireSurface();

            coreChart.Canvas.DisableAnimations = true;
            coreChart.IsLoaded = true;

            coreChart.Measure();
            coreChart.Canvas.DrawFrame(new ConsoleDrawingContext(CoreCanvas, surface, Background));

            coreChart.Unload();
            _surface = null; // unload disposes paint state, force a fresh surface next call.

            return surface.ToAnsi(home);
        }
    }

    /// <summary>
    /// Draws a single live frame. Does NOT disable animations or unload the chart, so successive
    /// calls drive the motion system and produce in-flight tween states. Cheap to call in a loop.
    /// </summary>
    public string RenderFrame(bool home = true)
    {
        var coreChart = GetCoreChart()
            ?? throw new InvalidOperationException("CoreChart is not available.");

        lock (SyncRoot)
        {
            var surface = AcquireSurface();

            // Idempotent — Load is invoked once in the constructor; this just guards re-entry.
            coreChart.IsLoaded = true;

            coreChart.Measure();
            coreChart.Canvas.DrawFrame(new ConsoleDrawingContext(CoreCanvas, surface, Background));

            return surface.ToAnsi(home);
        }
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
        if (_surface is null
            || _surface.Mode != _mode
            || _surface.Width != Width
            || _surface.Height != Height
            || (_mode == ConsoleRenderMode.Sixel && (_surface.CellWidth != SixelCellWidth || _surface.CellHeight != SixelCellHeight)))
        {
            _surface = new ConsoleSurface(Width, Height, _mode, SixelCellWidth, SixelCellHeight);
            UpdateGlobalCellSize();
        }
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

        ConfigureFromTerminalCells(cols, rows);
    }
}
