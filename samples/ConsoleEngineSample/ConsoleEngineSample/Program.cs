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
// --stackedcolumn / --stackedrow. Pick render mode with --braille / --sixel
// (default = half-block).
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

const int LinePoints = 64;
const int Bars = 16;
const int ScatterPoints = 24;

// One observable per "kind" we plot; only the selected kind's data updates each tick.
var lineData = new ObservableCollection<double>(SineWave(LinePoints, 1.0, 0));
var barData = new ObservableCollection<double>(RandomMags(Bars, 0.7));
var scatterData = new ObservableCollection<ObservablePoint>(RandomScatter(ScatterPoints));
var stack1Data = new ObservableCollection<double>(RandomMags(Bars, 0.5, seed: 11));
var stack2Data = new ObservableCollection<double>(RandomMags(Bars, 0.5, seed: 22));
var candleData = new ObservableCollection<FinancialPoint>(RandomCandles(Bars));
var boxData = new ObservableCollection<BoxValue>(RandomBoxes(Bars));

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
    if (cts.IsCancellationRequested) return; // second Ctrl+C: let the runtime kill us.
    e.Cancel = true;
    cts.Cancel();
};

_ = Task.Run(async () =>
{
    var rng = new Random();
    while (!cts.IsCancellationRequested)
    {
        try { await Task.Delay(1500, cts.Token); }
        catch (OperationCanceledException) { return; }

        lock (chart.SyncRoot)
        {
            for (var i = 0; i < lineData.Count; i++)
                lineData[i] = Math.Sin(2 * Math.PI * i / 16.0 + rng.NextDouble() * Math.PI * 2);
            for (var i = 0; i < barData.Count; i++) barData[i] = (rng.NextDouble() - 0.5) * 1.6;
            for (var i = 0; i < scatterData.Count; i++)
            {
                scatterData[i].X = rng.NextDouble() * (LinePoints - 1);
                scatterData[i].Y = (rng.NextDouble() - 0.5) * 1.8;
            }
            for (var i = 0; i < stack1Data.Count; i++) stack1Data[i] = rng.NextDouble() * 0.6;
            for (var i = 0; i < stack2Data.Count; i++) stack2Data[i] = rng.NextDouble() * 0.6;
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

static double[] SineWave(int n, double amp, double phase)
{
    var data = new double[n];
    for (var i = 0; i < n; i++) data[i] = amp * Math.Sin(2 * Math.PI * i / 16.0 + phase);
    return data;
}

static double[] RandomMags(int n, double amp, int seed = 1)
{
    var rng = new Random(seed);
    var data = new double[n];
    for (var i = 0; i < n; i++) data[i] = (rng.NextDouble() - 0.5) * 2 * amp;
    return data;
}

static ObservablePoint[] RandomScatter(int n)
{
    var rng = new Random(2);
    var data = new ObservablePoint[n];
    for (var i = 0; i < n; i++)
        data[i] = new ObservablePoint(rng.NextDouble() * (LinePoints - 1), (rng.NextDouble() - 0.5) * 1.6);
    return data;
}

static FinancialPoint[] RandomCandles(int n)
{
    var rng = new Random(3);
    var data = new FinancialPoint[n];
    var price = 100.0;
    for (var i = 0; i < n; i++)
    {
        var open = price;
        var close = open + (rng.NextDouble() - 0.5) * 6;
        var high = Math.Max(open, close) + rng.NextDouble() * 3;
        var low = Math.Min(open, close) - rng.NextDouble() * 3;
        data[i] = new FinancialPoint(DateTime.Today.AddDays(i), high, open, close, low);
        price = close;
    }
    return data;
}

static BoxValue[] RandomBoxes(int n)
{
    var rng = new Random(4);
    var data = new BoxValue[n];
    for (var i = 0; i < n; i++)
    {
        var center = 50 + rng.NextDouble() * 30;
        var spread = 5 + rng.NextDouble() * 10;
        var max = center + spread + rng.NextDouble() * 4;
        var q3 = center + spread * 0.5;
        var median = center;
        var q1 = center - spread * 0.5;
        var min = center - spread - rng.NextDouble() * 4;
        data[i] = new BoxValue(max, q3, q1, median, min);
    }
    return data;
}

static int? ParseIntFlag(string[] argv, string flag)
{
    for (var i = 0; i < argv.Length - 1; i++)
        if (argv[i] == flag && int.TryParse(argv[i + 1], out var v)) return v;
    return null;
}
