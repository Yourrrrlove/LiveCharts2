// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using System.Collections.ObjectModel;
using System.Text;
using LiveChartsCore;
using LiveChartsCore.Console;
using LiveChartsCore.Console.Painting;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;

// ----------------------------------------------------------------------------
// Renders a single chart in the terminal. Pick which series kind to render
// with --line / --column / --row / --scatter / --step / --stackedcolumn /
// --stackedrow / --stackedarea / --stackedsteparea / --candlestick / --box /
// --heat / --pie / --polar. Pick render mode with --halfblock / --braille /
// --sixel — default is auto-detect (Sixel if the terminal advertises it
// via DA1, otherwise Braille; HalfBlock stays opt-in). Cartesian kinds use
// a CartesianChart; --pie uses a PieChart; --polar uses a PolarChart.
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
         : args.Contains("--halfblock") ? ConsoleRenderMode.HalfBlock
         // Default: ask the terminal what it supports. DA1 + cap 4 = Sixel; otherwise we
         // fall through to Braille (safe everywhere a UTF-8 monospace font is). Detection
         // is a sub-100ms terminal round-trip, only happens once, and silently degrades
         // to Braille if stdout is redirected.
         : ConsoleTerminal.TryDetectSixelSupport() ? ConsoleRenderMode.Sixel
         : ConsoleRenderMode.Braille;

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
const int HeatCols = 7;         // heat — X grid size (one per day-of-week label)
const int HeatRows = 12;        // heat — Y grid size (one per month label)
string[] HeatXLabels = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
string[] HeatYLabels = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
const int PieSlices = 6;        // pie — number of wedges
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
var candleData = new ObservableCollection<FinancialPointI>(Candles(Bars, sharedOffset));
var boxData = new ObservableCollection<BoxValue>(Boxes(Bars, sharedOffset));
var heatData = new ObservableCollection<WeightedPoint>(Heat(HeatCols, HeatRows, sharedOffset));
var pieData = new ObservableValue[PieSlices];
for (var i = 0; i < PieSlices; i++) pieData[i] = new ObservableValue(SamplePieValue(i, sharedOffset));

var kind = SelectedKind(args);

// PieChart, PolarChart, and CartesianChart all share the same InMemoryConsoleChart base —
// they expose the same RenderMode / SixelCellWidth / Render / RenderLoop surface — so we
// keep the rest of the program polymorphic against that base after this branch.
InMemoryConsoleChart chart = kind switch
{
    "pie" => new PieChart
    {
        RenderMode = mode,
        Series = pieData
            .Select((pv, i) => (ISeries)new PieSeries<ObservableValue>(pv) { Name = $"S{i}" })
            .ToArray(),
    },
    "polar" => new PolarChart
    {
        RenderMode = mode,
        Series = [
            new PolarLineSeries<double>(lineData) { Name = "Signal", GeometrySize = 0, LineSmoothness = 0.65 }
        ],
    },
    "heat" => new CartesianChart
    {
        // Categorical labels demonstrate the BitmapFont glyph coverage in Sixel mode —
        // months / days exercise both upper- and lower-case letters.
        RenderMode = mode,
        Series = SelectSeries(args),
        XAxes = [new Axis { Labels = HeatXLabels }],
        YAxes = [new Axis { Labels = HeatYLabels }],
    },
    _ => new CartesianChart
    {
        RenderMode = mode,
        Series = SelectSeries(args),
    },
};

// Only set SixelCellWidth/Height when the user provided a CLI flag — otherwise leave them
// alone so the chart can auto-detect the terminal's real cell pixel size at first render.
// Setting either property (even to the same default) opts out of detection.
if (sixelCw.HasValue) chart.SixelCellWidth = sixelCw.Value;
if (sixelCh.HasValue) chart.SixelCellHeight = sixelCh.Value;

chart.ConfigureFromTerminalCells(cols, rows);

var forceLive = args.Contains("--live");
var forceSnapshot = args.Contains("--snapshot");

if (forceSnapshot || (!forceLive && System.Console.IsOutputRedirected))
{
    File.WriteAllText("chart.ansi", chart.Render(home: false));
    System.Console.WriteLine(
        $"Rendered {chart.Width}x{chart.Height} sub-pixels ({cols}x{rows} cells, mode={mode}, kind={kind}) to chart.ansi.");
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
        try { await Task.Delay(200, cts.Token); }
        catch (OperationCanceledException) { return; }

        sharedOffset += 0.025;

        lock (chart.SyncRoot)
        {
            ApplyBounce(lineData, PhaseLine, sharedOffset, signed: true);
            ApplyBounce(barData, PhaseBars, sharedOffset, signed: true);
            ApplyScatter(scatterData, sharedOffset);
            ApplyBounce(stack1Data, PhaseStackedA, sharedOffset, signed: false);
            ApplyBounce(stack2Data, PhaseStackedB, sharedOffset, signed: false);
            ApplyCandles(candleData, sharedOffset);
            ApplyBoxes(boxData, sharedOffset);
            ApplyHeat(heatData, HeatCols, HeatRows, sharedOffset);
            ApplyPie(pieData, sharedOffset);
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
    "stackedarea" => [
        new StackedAreaSeries<double>(stack1Data) { Name = "A" },
        new StackedAreaSeries<double>(stack2Data) { Name = "B" }
    ],
    "stackedsteparea" => [
        new StackedStepAreaSeries<double>(stack1Data) { Name = "A" },
        new StackedStepAreaSeries<double>(stack2Data) { Name = "B" }
    ],
    "candlestick" => [
        new CandlestickSeries<FinancialPointI>(candleData) { Name = "OHLC" }
    ],
    "box" => [
        new BoxSeries<BoxValue>(boxData) { Name = "Distribution" }
    ],
    "heat" => [
        new HeatSeries<WeightedPoint>(heatData) { Name = "Heat" }
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
    if (argv.Contains("--stackedarea")) return "stackedarea";
    if (argv.Contains("--stackedsteparea")) return "stackedsteparea";
    if (argv.Contains("--candlestick")) return "candlestick";
    if (argv.Contains("--box")) return "box";
    if (argv.Contains("--heat")) return "heat";
    if (argv.Contains("--pie")) return "pie";
    if (argv.Contains("--polar")) return "polar";
    return "line";
}

// ----------------------------------------------------------------------------
// Data shapers — sum of three phase-shifted sines at golden-ratio harmonics.
// The traveling-wave term (-t * speed) makes each frequency component scroll
// across the chart at a slightly different rate, so the curve never quite
// loops onto itself and the eye sees an organic, evolving pattern.
// "signed" leaves the result in roughly [-1, 1]; otherwise it's mapped to [0, 1].
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

static double SampleBounce(double i, int n, double phase, double offset, bool signed)
{
    var x = i / (double)(n - 1);
    var t = offset * 2 * Math.PI;
    var p = phase * 2 * Math.PI;

    var w = 0.65 * Math.Sin(2 * Math.PI * 1.000 * x - t * 1.0 + p)
          + 0.30 * Math.Sin(2 * Math.PI * 1.618 * x - t * 0.7 + p * 1.3)
          + 0.10 * Math.Sin(2 * Math.PI * 2.618 * x - t * 1.3 + p * 0.5);

    return signed ? w : (w + 1) * 0.5; // roughly [0, 1] for unsigned (stacked).
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
static FinancialPointI[] Candles(int n, double offset)
{
    var data = new FinancialPointI[n];
    for (var i = 0; i < n; i++) data[i] = CandlePoint(i, n, offset);
    return data;
}

static void ApplyCandles(IList<FinancialPointI> dst, double offset)
{
    // Mutate in-place rather than replacing the FinancialPointI instance: replacement
    // fires a Replace notification that the chart treats as "remove + add a different
    // entity", which restarts the candle's tween from zero every tick. Property mutation
    // triggers INotifyPropertyChanged on the same entity, so the existing tween redirects
    // smoothly to the new target without losing its mid-flight position.
    for (var i = 0; i < dst.Count; i++)
    {
        var existing = dst[i];
        var next = CandlePoint(i, dst.Count, offset);
        existing.High = next.High;
        existing.Open = next.Open;
        existing.Close = next.Close;
        existing.Low = next.Low;
    }
}

static FinancialPointI CandlePoint(int i, int n, double offset)
{
    // Open and close are samples from the *same* smooth wave taken half a step apart —
    // each candle's up/down direction is just the local slope of one continuous curve,
    // so adjacent candles correlate and the body color changes gradually as the wave
    // scrolls. The original formulation (open from i-1, close from i) read two
    // independent wave samples; correct in the limit but the slope at each i changed
    // faster than a viewer's "candle expectation", which made bodies flip erratically.
    var open = 100 + SampleBounce(i, n, 0.05, offset, signed: true) * 5;
    var close = 100 + SampleBounce(i + 0.5, n, 0.05, offset, signed: true) * 5;
    var high = Math.Max(open, close) + 1.5 + SampleBounce(i, n, 0.30, offset, signed: false) * 1.5;
    var low = Math.Min(open, close) - 1.5 - SampleBounce(i, n, 0.40, offset, signed: false) * 1.5;
    return new FinancialPointI(high, open, close, low);
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
    // Same in-place mutation as ApplyCandles — see comment there for the reasoning.
    for (var i = 0; i < dst.Count; i++)
    {
        var existing = dst[i];
        var next = BoxPoint(i, dst.Count, offset);
        existing.Max = next.Max;
        existing.ThirdQuartile = next.ThirdQuartile;
        existing.Median = next.Median;
        existing.FirtQuartile = next.FirtQuartile;
        existing.Min = next.Min;
    }
}

static BoxValue BoxPoint(int i, int n, double offset)
{
    var center = 70 + SampleBounce(i, n, 0.05, offset, signed: true) * 10;
    var spread = 4 + SampleBounce(i, n, 0.30, offset, signed: false) * 6;
    // BoxValue ctor is (max, thirdQuartile, firstQuartile, min, median) — not the order
    // most box-plot libraries use. Use named args so the next reader doesn't get tripped.
    return new BoxValue(
        max: center + spread + 2,
        thirdQuartile: center + spread / 2,
        firstQuartile: center - spread / 2,
        min: center - spread - 2,
        median: center);
}

// Pie — slice values are independent slow oscillators around a positive midpoint, so wedge
// sizes shift but stay roughly proportional. Each slice gets its own ObservableValue and is
// mutated in place (Value setter triggers INPC; the chart redirects the in-flight tween).
static void ApplyPie(ObservableValue[] data, double offset)
{
    for (var i = 0; i < data.Length; i++) data[i].Value = SamplePieValue(i, offset);
}

static double SamplePieValue(int i, double offset)
{
    var t = offset * 2 * Math.PI;
    // Floor of 1 keeps every wedge visible; oscillation amplitude of 4 means the relative
    // share of any one wedge can roughly double or halve as the offset advances.
    return 1 + (Math.Sin(t * 0.6 + i * 0.8) + 1) * 2;
}

// Heat — 2D grid of WeightedPoint(x, y, weight). Weight is driven by a 2D scrolling wave so
// the heatmap "breathes" with the offset. X and Y are fixed grid coords (set once); only the
// Weight property mutates each tick.
static WeightedPoint[] Heat(int cols, int rows, double offset)
{
    var data = new WeightedPoint[cols * rows];
    for (var x = 0; x < cols; x++)
        for (var y = 0; y < rows; y++)
            data[x * rows + y] = new WeightedPoint(x, y, HeatWeight(x, y, cols, rows, offset));
    return data;
}

static void ApplyHeat(IList<WeightedPoint> dst, int cols, int rows, double offset)
{
    for (var x = 0; x < cols; x++)
        for (var y = 0; y < rows; y++)
            dst[x * rows + y].Weight = HeatWeight(x, y, cols, rows, offset);
}

static double HeatWeight(int xi, int yi, int cols, int rows, double offset)
{
    var x = xi / (double)(cols - 1);
    var y = yi / (double)(rows - 1);
    var t = offset * 2 * Math.PI;
    // Two phase-shifted 2D sines — each travels in its own direction so the gradient
    // doesn't repeat as a simple horizontal slide.
    var w = 0.6 * Math.Sin(2 * Math.PI * x - t) * Math.Cos(2 * Math.PI * y - t * 0.5)
          + 0.4 * Math.Sin(2 * Math.PI * 1.5 * x + 2 * Math.PI * y - t * 0.7);
    return (w + 1) * 0.5; // map to roughly [0, 1]
}

static int? ParseIntFlag(string[] argv, string flag)
{
    for (var i = 0; i < argv.Length - 1; i++)
        if (argv[i] == flag && int.TryParse(argv[i + 1], out var v)) return v;
    return null;
}
