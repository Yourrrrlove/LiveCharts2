// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using System.Text;
using LiveChartsCore;
using LiveChartsCore.Console;
using LiveChartsCore.Console.Painting;

// ----------------------------------------------------------------------------
// Proof of concept: a CartesianChart rendered straight to the terminal using
// half-block characters and 24-bit ANSI color, with NO SkiaSharp involved.
// ----------------------------------------------------------------------------

System.Console.OutputEncoding = Encoding.UTF8;

LiveCharts.Configure(c => c
    .AddConsole()
    .AddConsoleDefaultTheme()
    .AddDefaultMappers());

// Pick a size from the current terminal when one is attached; otherwise fall back to a
// fixed grid so the sample also works when stdout is redirected.
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

var chart = new CartesianChart
{
    Width = cols,
    Height = rows * 2,
    Background = new(0, 0, 0),
    Series =
    [
        new LineSeries<double>(SineWave(64, 1.0, 0))
        {
            Stroke = new SolidColorPaint(new(80, 200, 255), 1f),
            Fill = null,
            GeometrySize = 0,
            LineSmoothness = 0.65
        },
        new LineSeries<double>(SineWave(64, 0.6, Math.PI / 2))
        {
            Stroke = new SolidColorPaint(new(255, 140, 80), 1f),
            Fill = null,
            GeometrySize = 0,
            LineSmoothness = 0.65
        }
    ]
};

var ansi = chart.Render(home: false);

// Always dump the encoded frame for inspection (handy when running headless).
File.WriteAllText("chart.ansi", ansi);

if (!System.Console.IsOutputRedirected)
{
    System.Console.Clear();
    System.Console.Out.Write(ansi);
    System.Console.WriteLine();
}
else
{
    System.Console.WriteLine($"Rendered {cols}x{rows*2} sub-pixels ({cols}x{rows} cells) to chart.ansi.");
}

static double[] SineWave(int n, double amp, double phase)
{
    var data = new double[n];
    for (var i = 0; i < n; i++)
        data[i] = amp * Math.Sin(2 * Math.PI * i / 16.0 + phase);
    return data;
}
