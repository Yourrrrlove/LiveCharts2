// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using System.Collections.ObjectModel;
using System.Text;
using LiveChartsCore;
using LiveChartsCore.Console;
using LiveChartsCore.Console.Painting;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;

// ----------------------------------------------------------------------------
// Renders a single CartesianChart series in the terminal. Pick which series
// kind to render with --line / --column / --row / --scatter / --step /
// --stackedcolumn / --stackedrow / --candlestick / --box. Pick render mode
// with --braille / --sixel (default = half-block).
//
// Data is sampled from EasingFunctions.BounceInOut shifted by a shared offset
// that advances every tick — produces a clean wave that scrolls across the
// chart instead of jittering randomly.
//
//   * If stdout is a terminal       → live animated chart (Ctrl+C to exit;
//                                      second Ctrl+C force-kills).
//   * If stdout is redirected/pipe  → one-shot snapshot to chart.ansi.
// ----------------------------------------------------------------------------

System.Console.OutputEncoding = Encoding.UTF8;

LiveCharts.Configure(c => c
    .AddConsole()
    .AddConsoleDefaultTheme()
    .AddDefaultMappers());

var mode = args.Contains("--sixel") ? ConsoleRenderMode.Sixel
         : args.Contains("--braille") ? ConsoleRenderMode.Braille
         : ConsoleRenderMode.HalfBlock;

int? sixelCw = ParseIntFlag(args, "--sixel-cw");
int? sixelCh = ParseIntFlag(args, "--sixel-ch");

int cols, rows;
try
{
    cols = Math.Max(40, System.Console.WindowWidth - 1);
    rows = Math.Max(10, System.Console.WindowHeight - 2);
}
catch (System.IO.IOException) { cols = 120; rows = 30; }

const int Points = 32;          // line/step
const int Bars = 16;            // column/row/stacked
const int ScatterPoints = 24;
const double PhaseLine = 0;
const double PhaseBars = 0.15;
const double PhaseStackedA = 0;
const double PhaseStackedB = 0.5;

var sharedOffset = 0.0;

var lineData = new ObservableCollection<double>(Bounce(Points, PhaseLine, sharedOffset, signed: true));
var barData = new ObservableCollection<double>(Bounce(Bars, PhaseBars, sharedOffset, signed: true));
var scatterData = new ObservableCollection<ObservablePoint>(Scatter(ScatterPoints, sharedOffset));
var stack1Data = new ObservableCollection<double>(Bounce(Bars, PhaseStackedA, sharedOffset, signed: false));
var stack2Data = new ObservableCollection<double>(Bounce(Bars, PhaseStackedB, sharedOffset, signed: false));
var candleData = new ObservableCollection<FinancialPoint>(Candles(Bars, sharedOffset));
var boxData = new ObservableCollection<BoxValue>(Boxes(Bars, sharedOffset));

ISeries[] series = SelectSeries(args);

var chart = new CartesianChart
{
    RenderMode = mode,
    SixelCellWidth = sixelCw ?? 10,
    SixelCellHeight = sixelCh ?? 22,
    Series = series
};

chart.ConfigureFromTerminalCells(cols, rows);

var forceLive = args.Contains("--live");
var forceSnapshot = args.Contains("--snapshot");

if (forceSnapshot || (!forceLive && System.Console.IsOutputRedirected))
{
    File.WriteAllText("chart.ansi", chart.Render(home: false));
    System.Console.WriteLine(
        $"Rendered {chart.Width}x{chart.Height} sub-pixels ({cols}x{rows} cells, mode={mode}, kind={SelectedKind(args)}) to chart.ansi.");
    return;
}

using var cts = new CancellationTokenSource();
System.Console.CancelKeyPress += (_, e) =>
{
    if (cts.IsCancellationRequested) return;
    e.Cancel = true;
    cts.Cancel();
};

// Data driver — bumps the shared offset by a small step every 250ms. The chart's animation
// system tweens between snapshots so the visual is smooth at the render FPS.
_ = Task.Run(async () =>
{
    while (!cts.IsCancellationRequested)
    {
        try { await Task.Delay(250, cts.Token); }
        catch (OperationCanceledException) { return; }

        sharedOffset += 0.04;

        lock (chart.SyncRoot)
        {
            ApplyBounce(lineData, PhaseLine, sharedOffset, signed: true);
            ApplyBounce(barData, PhaseBars, sharedOffset, signed: true);
            ApplyScatter(scatterData, sharedOffset);
            ApplyBounce(stack1Data, PhaseStackedA, sharedOffset, signed: false);
            ApplyBounce(stack2Data, PhaseStackedB, sharedOffset, signed: false);
            ApplyCandles(candleData, sharedOffset);
            ApplyBoxes(boxData, sharedOffset);
        }
    }
});

await chart.RenderLoopAsync(fps: 30, ct: cts.Token);

ISeries[] SelectSeries(string[] argv) => SelectedKind(argv) switch
{
    "column" => [
        new ColumnSeries<double>(barData) { Name = "Bars" }
    ],
    "row" => [
        new RowSeries<double>(barData) { Name = "Bars" }
    ],
    "scatter" => [
        new ScatterSeries<ObservablePoint>(scatterData) { Name = "Scatter", GeometrySize = 8 }
    ],
    "step" => [
        new StepLineSeries<double>(lineData) { Name = "Signal", GeometrySize = 0 }
    ],
    "stackedcolumn" => [
        new StackedColumnSeries<double>(stack1Data) { Name = "A" },
        new StackedColumnSeries<double>(stack2Data) { Name = "B" }
    ],
    "stackedrow" => [
        new StackedRowSeries<double>(stack1Data) { Name = "A" },
        new StackedRowSeries<double>(stack2Data) { Name = "B" }
    ],
    "candlestick" => [
        new CandlestickSeries<FinancialPoint>(candleData) { Name = "OHLC" }
    ],
    "box" => [
        new BoxSeries<BoxValue>(boxData) { Name = "Distribution" }
    ],
    _ => [
        new LineSeries<double>(lineData) { Name = "Signal", GeometrySize = 0, LineSmoothness = 0.65 }
    ]
};

static string SelectedKind(string[] argv)
{
    if (argv.Contains("--column")) return "column";
    if (argv.Contains("--row")) return "row";
    if (argv.Contains("--scatter")) return "scatter";
    if (argv.Contains("--step")) return "step";
    if (argv.Contains("--stackedcolumn")) return "stackedcolumn";
    if (argv.Contains("--stackedrow")) return "stackedrow";
    if (argv.Contains("--candlestick")) return "candlestick";
    if (argv.Contains("--box")) return "box";
    return "line";
}

// ----------------------------------------------------------------------------
// Data shapers — all sample EasingFunctions.BounceInOut at (i / (n-1) + phase
// + offset), wrapped to [0, 1]. "signed" centers the result around zero.
// ----------------------------------------------------------------------------

static double[] Bounce(int n, double phase, double offset, bool signed)
{
    var data = new double[n];
    for (var i = 0; i < n; i++) data[i] = SampleBounce(i, n, phase, offset, signed);
    return data;
}

static void ApplyBounce(IList<double> dst, double phase, double offset, bool signed)
{
    for (var i = 0; i < dst.Count; i++) dst[i] = SampleBounce(i, dst.Count, phase, offset, signed);
}

static double SampleBounce(int i, int n, double phase, double offset, bool signed)
{
    var x = i / (double)(n - 1) + phase + offset;
    x = ((x % 1) + 1) % 1; // wrap to [0, 1]
    var v = EasingFunctions.BounceInOut((float)x);
    return signed ? v * 2 - 1 : v;
}

// Scatter — X bounces along one phase, Y along another, so points trace a
// Lissajous-ish path that breathes with the offset.
static ObservablePoint[] Scatter(int n, double offset)
{
    var data = new ObservablePoint[n];
    for (var i = 0; i < n; i++) data[i] = ScatterPoint(i, n, offset);
    return data;
}

static void ApplyScatter(IList<ObservablePoint> dst, double offset)
{
    for (var i = 0; i < dst.Count; i++)
    {
        var p = ScatterPoint(i, dst.Count, offset);
        dst[i].X = p.X;
        dst[i].Y = p.Y;
    }
}

static ObservablePoint ScatterPoint(int i, int n, double offset)
{
    // Spread X across the line range so it overlays cleanly on the same axis.
    var x = i / (double)(n - 1) * (Points - 1);
    var y = SampleBounce(i, n, 0.10 + (i & 1) * 0.20, offset, signed: true);
    return new ObservablePoint(x, y);
}

// Candlesticks — bounce drives the close price; high/low expand around it.
static FinancialPoint[] Candles(int n, double offset)
{
    var data = new FinancialPoint[n];
    for (var i = 0; i < n; i++) data[i] = CandlePoint(i, n, offset);
    return data;
}

static void ApplyCandles(IList<FinancialPoint> dst, double offset)
{
    for (var i = 0; i < dst.Count; i++)
    {
        var c = CandlePoint(i, dst.Count, offset);
        dst[i] = c;
    }
}

static FinancialPoint CandlePoint(int i, int n, double offset)
{
    var close = 100 + SampleBounce(i, n, 0.05, offset, signed: true) * 5;
    var open = 100 + SampleBounce(i - 1, n, 0.05, offset, signed: true) * 5;
    var high = Math.Max(open, close) + 1.5 + SampleBounce(i, n, 0.30, offset, signed: false) * 1.5;
    var low = Math.Min(open, close) - 1.5 - SampleBounce(i, n, 0.40, offset, signed: false) * 1.5;
    return new FinancialPoint(DateTime.Today.AddDays(i), high, open, close, low);
}

// Boxes — bounced median + bounce-modulated spread so the boxes breathe in/out.
static BoxValue[] Boxes(int n, double offset)
{
    var data = new BoxValue[n];
    for (var i = 0; i < n; i++) data[i] = BoxPoint(i, n, offset);
    return data;
}

static void ApplyBoxes(IList<BoxValue> dst, double offset)
{
    for (var i = 0; i < dst.Count; i++) dst[i] = BoxPoint(i, dst.Count, offset);
}

static BoxValue BoxPoint(int i, int n, double offset)
{
    var center = 70 + SampleBounce(i, n, 0.05, offset, signed: true) * 10;
    var spread = 4 + SampleBounce(i, n, 0.30, offset, signed: false) * 6;
    return new BoxValue(center + spread + 2, center + spread / 2, center - spread / 2, center, center - spread - 2);
}

static int? ParseIntFlag(string[] argv, string flag)
{
    for (var i = 0; i < argv.Length - 1; i++)
        if (argv[i] == flag && int.TryParse(argv[i + 1], out var v)) return v;
    return null;
}
