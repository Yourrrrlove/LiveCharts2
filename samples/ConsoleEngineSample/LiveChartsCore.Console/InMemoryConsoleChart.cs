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
    private LvcColor _background = default; // A=0 → "transparent / not set" sentinel.
    private bool _backgroundExplicit;
    private bool _backgroundDetected;
    private bool _sixelCellSizeExplicit;
    private bool _sixelCellSizeDetected;
    private int _sixelCellWidth = 10;
    private int _sixelCellHeight = 22;

    public CoreMotionCanvas CoreCanvas { get; } = new();

    /// <summary>
    /// Width in sub-pixels (= cells × <see cref="ConsoleSurface.CellWidth"/> for the current mode).
    /// </summary>
    public int Width { get; set; } = 120;

    /// <summary>
    /// Height in sub-pixels (= cells × <see cref="ConsoleSurface.CellHeight"/> for the current mode).
    /// </summary>
    public int Height { get; set; } = 40;

    /// <summary>
    /// Background color. Defaults to "transparent" (A = 0) so the terminal background shows
    /// through. The chart will try to detect the actual terminal background once on first
    /// render via OSC 11 and replace this with a solid color when detection succeeds. Set this
    /// explicitly to opt out of detection.
    /// </summary>
    public LvcColor Background
    {
        get => _background;
        set { _background = value; _backgroundExplicit = true; }
    }

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
    /// Pixels per terminal cell when in <see cref="ConsoleRenderMode.Sixel"/>. If unset, the
    /// chart queries the terminal at first render via <c>\x1b[16t</c> and uses its reported
    /// cell pixel size — this aligns the image to cell boundaries and prevents the empty
    /// strip that otherwise appears below the chart when the assumed and actual cell heights
    /// differ. If detection fails (e.g., the terminal doesn't support the report), the
    /// defaults below are used. Setting either value explicitly opts out of detection.
    /// </summary>
    public int SixelCellWidth
    {
        get => _sixelCellWidth;
        set { _sixelCellWidth = value; _sixelCellSizeExplicit = true; }
    }

    public int SixelCellHeight
    {
        get => _sixelCellHeight;
        set { _sixelCellHeight = value; _sixelCellSizeExplicit = true; }
    }

    /// <summary>
    /// Sizes <see cref="Width"/> and <see cref="Height"/> so the chart fills the given number of
    /// terminal cells under the current <see cref="RenderMode"/>.
    /// </summary>
    public void ConfigureFromTerminalCells(int cellCols, int cellRows)
    {
        // Resolve cell size BEFORE deriving Width/Height — otherwise we'd size the surface
        // using the assumed defaults, then detect a different real cell size on first
        // render, and end up with a misaligned image until the next configure.
        EnsureSixelCellSizeResolved();

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
        // character. For cell-grid modes this is the cell pixel size (1 char = 1 cell). For
        // Sixel, labels are bitmap-rendered, so this is the bitmap glyph cell instead — it
        // diverges from the terminal cell pixel size used to size the Sixel image overall.
        var (w, h) = _mode switch
        {
            ConsoleRenderMode.Braille => (2, 4),
            ConsoleRenderMode.Sixel => (BitmapFont.CellWidth(), BitmapFont.CellHeight()),
            _ => (1, 2),
        };
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
            EnsureBackgroundResolved();
            EnsureSixelCellSizeResolved();
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
            EnsureBackgroundResolved();
            EnsureSixelCellSizeResolved();
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

        // \x1b[0m up-front: \x1b[2J erases the screen using the *currently active* SGR
        // background — if PowerShell's prompt or any prior output left a non-default bg
        // selected, the cleared screen would inherit it. Reset SGR first so the clear
        // uses the terminal's actual default background.
        await output.WriteAsync("\x1b[0m\x1b[?25l\x1b[2J");

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

    private void EnsureBackgroundResolved()
    {
        // If the user set Background explicitly, honor it. Otherwise try ONCE to detect the
        // terminal's real background via OSC 11. If detection fails, leave it transparent so
        // the cell encoder emits default-attr escapes and Sixel uses Pb=1 — either way, the
        // terminal's own background shows through.
        if (_backgroundExplicit || _backgroundDetected) return;
        _backgroundDetected = true;

        var detected = ConsoleTerminal.TryDetectBackground();
        if (detected.HasValue)
        {
            var c = detected.Value;
            _background = new LvcColor(c.R, c.G, c.B); // force opaque so the encoder paints it.
        }
    }

    private void EnsureSixelCellSizeResolved()
    {
        // Only meaningful in Sixel mode; cell-grid modes use fixed sub-pixel ratios.
        if (_mode != ConsoleRenderMode.Sixel) return;
        if (_sixelCellSizeExplicit || _sixelCellSizeDetected) return;
        _sixelCellSizeDetected = true;

        var detected = ConsoleTerminal.TryDetectCellPixelSize();
        if (!detected.HasValue) return;

        // Set fields directly — the property setters would mark the values as "explicit"
        // and disable redetection if the user later switches modes.
        _sixelCellWidth = detected.Value.width;
        _sixelCellHeight = detected.Value.height;
    }

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
