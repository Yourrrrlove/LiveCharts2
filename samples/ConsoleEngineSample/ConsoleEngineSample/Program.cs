// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using System.Collections.ObjectModel;
using System.Text;
using LiveChartsCore;
using LiveChartsCore.Console;
using LiveChartsCore.Console.Painting;
using LiveChartsCore.Defaults;

// ----------------------------------------------------------------------------
// Proof of concept: a CartesianChart rendered straight to the terminal using
// half-block characters and 24-bit ANSI color, with NO SkiaSharp involved.
//
//   * If stdout is a terminal       → live animated chart (Ctrl+C to exit).
//   * If stdout is redirected/pipe  → one-shot snapshot to chart.ansi.
//
// Series: a smooth line, a column histogram, and scatter points — all sharing
// the same axes and animating in sync.
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
catch (System.IO.IOException)
{
    cols = 120;
    rows = 30;
}

const int LinePoints = 64;
const int Bars = 12;
const int ScatterPoints = 24;

var lineData = new ObservableCollection<double>(SineWave(LinePoints, 1.0, 0));
var barData = new ObservableCollection<double>(RandomMagnitudes(Bars, 0.6));
var scatterData = new ObservableCollection<ObservablePoint>(RandomScatter(ScatterPoints));

var chart = new CartesianChart
{
    RenderMode = mode,
    SixelCellWidth = sixelCw ?? 10,
    SixelCellHeight = sixelCh ?? 22,
    // Background defaults to auto-detected terminal background; set explicitly to override.
    Series =
    [
        new LineSeries<double>(lineData)
        {
            Name = "Signal",
            Stroke = new SolidColorPaint(new(80, 200, 255), 1f),
            Fill = null,
            GeometrySize = 0,
            LineSmoothness = 0.65
        },
        new ColumnSeries<double>(barData)
        {
            Name = "Bars",
            Fill = new SolidColorPaint(new(255, 140, 80, 200))
        },
        new ScatterSeries<ObservablePoint>(scatterData)
        {
            Name = "Scatter",
            Fill = new SolidColorPaint(new(120, 220, 120)),
            GeometrySize = 8
        }
    ]
};

chart.ConfigureFromTerminalCells(cols, rows);

var forceLive = args.Contains("--live");
var forceSnapshot = args.Contains("--snapshot");

if (forceSnapshot || (!forceLive && System.Console.IsOutputRedirected))
{
    File.WriteAllText("chart.ansi", chart.Render(home: false));
    System.Console.WriteLine(
        $"Rendered {chart.Width}x{chart.Height} sub-pixels ({cols}x{rows} cells, mode={mode}) to chart.ansi.");
    return;
}

using var cts = new CancellationTokenSource();
System.Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// Re-randomize each series every 1.5s; the chart's animation system tweens between states.
// Mutations from this background thread must hold chart.SyncRoot — see the SyncRoot doc.
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

            for (var i = 0; i < barData.Count; i++)
                barData[i] = (rng.NextDouble() - 0.5) * 1.4;

            for (var i = 0; i < scatterData.Count; i++)
            {
                scatterData[i].X = rng.NextDouble() * (LinePoints - 1);
                scatterData[i].Y = (rng.NextDouble() - 0.5) * 1.6;
            }
        }
    }
});

await chart.RenderLoopAsync(fps: 30, ct: cts.Token);

static double[] SineWave(int n, double amp, double phase)
{
    var data = new double[n];
    for (var i = 0; i < n; i++)
        data[i] = amp * Math.Sin(2 * Math.PI * i / 16.0 + phase);
    return data;
}

static double[] RandomMagnitudes(int n, double amp)
{
    var rng = new Random(1);
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

static int? ParseIntFlag(string[] argv, string flag)
{
    for (var i = 0; i < argv.Length - 1; i++)
        if (argv[i] == flag && int.TryParse(argv[i + 1], out var v)) return v;
    return null;
}
