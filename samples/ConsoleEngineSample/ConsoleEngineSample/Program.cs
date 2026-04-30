// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using System.Collections.ObjectModel;
using System.Text;
using LiveChartsCore;
using LiveChartsCore.Console;
using LiveChartsCore.Console.Painting;

// ----------------------------------------------------------------------------
// Proof of concept: a CartesianChart rendered straight to the terminal using
// half-block characters and 24-bit ANSI color, with NO SkiaSharp involved.
//
//   * If stdout is a terminal       → live animated chart (Ctrl+C to exit).
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

var data1 = new ObservableCollection<double>(SineWave(64, 1.0, 0));
var data2 = new ObservableCollection<double>(SineWave(64, 0.6, Math.PI / 2));

var chart = new CartesianChart
{
    RenderMode = mode,
    Background = new(0, 0, 0),
    Series =
    [
        new LineSeries<double>(data1)
        {
            Name = "Signal A",
            Stroke = new SolidColorPaint(new(80, 200, 255), 1f),
            Fill = null,
            GeometrySize = 0,
            LineSmoothness = 0.65
        },
        new LineSeries<double>(data2)
        {
            Name = "Signal B",
            Stroke = new SolidColorPaint(new(255, 140, 80), 1f),
            Fill = null,
            GeometrySize = 0,
            LineSmoothness = 0.65
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

// Background driver — re-randomize the two signals every 1.5 s. The chart's animation
// system tweens the column heights / line points smoothly between updates.
_ = Task.Run(async () =>
{
    var rng = new Random();
    while (!cts.IsCancellationRequested)
    {
        try { await Task.Delay(1500, cts.Token); }
        catch (OperationCanceledException) { return; }

        // Mutations from a non-render thread must hold chart.SyncRoot — otherwise the chart's
        // data factory can blow up mid-Measure with "Collection was modified".
        lock (chart.SyncRoot)
        {
            for (var i = 0; i < data1.Count; i++)
                data1[i] = Math.Sin(2 * Math.PI * i / 16.0 + rng.NextDouble() * Math.PI * 2);
            for (var i = 0; i < data2.Count; i++)
                data2[i] = 0.7 * Math.Sin(2 * Math.PI * i / 14.0 + rng.NextDouble() * Math.PI * 2);
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
