// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LiveChartsCore;
using LiveChartsCore.Console;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;

// ----------------------------------------------------------------------------
// `lvc` — render LiveCharts to the terminal from a JSON chart spec. The intent
// is for AI-driven clients (Claude Code, MCP-aware agents, anything that can
// shell out) to ship terminal charts without integrating the .NET library
// directly. Read JSON on stdin or via --json, write ANSI to stdout.
//
//   echo '{"kind":"line","series":[{"values":[1,2,3,4,3,2,1]}]}' | lvc
//   lvc --json '{"kind":"pie","title":"Slices","series":[{"name":"A","values":[1]},{"name":"B","values":[3]}]}'
//   lvc --file chart.json --mode sixel
//
// Default mode is auto-detect (Sixel if the terminal advertises it via DA1,
// otherwise Braille). Override with --mode halfblock|braille|sixel.
// ----------------------------------------------------------------------------

if (args.Contains("--help") || args.Contains("-h"))
{
    PrintUsage();
    return 0;
}

System.Console.OutputEncoding = Encoding.UTF8;

string? jsonInput = null;
ConsoleRenderMode? modeOverride = null;
int? widthOverride = null;
int? heightOverride = null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--json" when i + 1 < args.Length:
            jsonInput = args[++i];
            break;
        case "--file" when i + 1 < args.Length:
            jsonInput = File.ReadAllText(args[++i]);
            break;
        case "--mode" when i + 1 < args.Length:
            modeOverride = args[++i].ToLowerInvariant() switch
            {
                "sixel" => ConsoleRenderMode.Sixel,
                "braille" => ConsoleRenderMode.Braille,
                "halfblock" => ConsoleRenderMode.HalfBlock,
                "auto" => null,
                _ => throw new ArgumentException($"Unknown --mode: {args[i]}"),
            };
            break;
        case "--width" when i + 1 < args.Length:
            widthOverride = int.Parse(args[++i]);
            break;
        case "--height" when i + 1 < args.Length:
            heightOverride = int.Parse(args[++i]);
            break;
        default:
            // Unknown flag — silently ignore so additions don't break older callers.
            break;
    }
}

// stdin fallback for the JSON spec — natural for shell pipelines.
if (jsonInput is null && System.Console.IsInputRedirected)
    jsonInput = System.Console.In.ReadToEnd();

if (string.IsNullOrWhiteSpace(jsonInput))
{
    System.Console.Error.WriteLine("error: no chart spec provided. Pipe JSON on stdin, or use --json / --file.");
    PrintUsage();
    return 2;
}

ChartSpec spec;
try
{
    spec = JsonSerializer.Deserialize(jsonInput, ChartJsonContext.Default.ChartSpec)
        ?? throw new InvalidOperationException("spec deserialized to null");
}
catch (JsonException ex)
{
    System.Console.Error.WriteLine($"error: invalid JSON — {ex.Message}");
    return 2;
}

LiveCharts.Configure(c => c
    .AddConsole()
    .AddConsoleDefaultTheme()
    .AddDefaultMappers());

// Resolve render mode. Explicit > spec > auto-detect (DA1 → Sixel, else Braille).
// Auto-detect requires a real TTY; with output redirected we fall through to Braille
// (valid on any UTF-8 terminal that later cats the file).
var mode = modeOverride
    ?? (System.Console.IsOutputRedirected
        ? ConsoleRenderMode.Braille
        : ConsoleTerminal.TryDetectSixelSupport() ? ConsoleRenderMode.Sixel : ConsoleRenderMode.Braille);

// Cell-based size — fall back to terminal size, then 120x30 if the terminal isn't a TTY
// (output redirected, etc.). Spec width/height are in cells.
int cols, rows;
try
{
    cols = widthOverride ?? spec.Width ?? Math.Max(40, System.Console.WindowWidth - 1);
    rows = heightOverride ?? spec.Height ?? Math.Max(10, System.Console.WindowHeight - 2);
}
catch (System.IO.IOException)
{
    cols = widthOverride ?? spec.Width ?? 120;
    rows = heightOverride ?? spec.Height ?? 30;
}

InMemoryConsoleChart chart;
try { chart = BuildChart(spec, mode); }
catch (Exception ex)
{
    System.Console.Error.WriteLine($"error: {ex.Message}");
    return 2;
}

if (!string.IsNullOrEmpty(spec.Title)) chart.TitleText = spec.Title;
chart.ConfigureFromTerminalCells(cols, rows);
System.Console.Out.Write(chart.Render(home: false));
return 0;

// ----------------------------------------------------------------------------

static InMemoryConsoleChart BuildChart(ChartSpec spec, ConsoleRenderMode mode) => spec.Kind?.ToLowerInvariant() switch
{
    "line"    => Cartesian(mode, spec, s => new LineSeries<double>(s.Values ?? []) { Name = s.Name, GeometrySize = 0, LineSmoothness = 0.6 }),
    "column"  => Cartesian(mode, spec, s => new ColumnSeries<double>(s.Values ?? []) { Name = s.Name }),
    "row"     => Cartesian(mode, spec, s => new RowSeries<double>(s.Values ?? []) { Name = s.Name }),
    "step"    => Cartesian(mode, spec, s => new StepLineSeries<double>(s.Values ?? []) { Name = s.Name, GeometrySize = 0 }),
    "stackedcolumn" => Cartesian(mode, spec, s => new StackedColumnSeries<double>(s.Values ?? []) { Name = s.Name }),
    "stackedrow"    => Cartesian(mode, spec, s => new StackedRowSeries<double>(s.Values ?? []) { Name = s.Name }),
    "stackedarea"   => Cartesian(mode, spec, s => new StackedAreaSeries<double>(s.Values ?? []) { Name = s.Name }),
    "scatter" => Cartesian(mode, spec, s => new ScatterSeries<ObservablePoint>(
        (s.Points ?? []).Select(p => new ObservablePoint(p[0], p[1])).ToArray()) { Name = s.Name }),
    "candlestick" => Cartesian(mode, spec, s => new CandlestickSeries<FinancialPointI>(
        (s.Points ?? []).Select(p => new FinancialPointI(p[1], p[0], p[3], p[2])).ToArray()) { Name = s.Name }),
    "box" => Cartesian(mode, spec, s => new BoxSeries<BoxValue>(
        (s.Points ?? []).Select(p => new BoxValue(max: p[0], thirdQuartile: p[1], firstQuartile: p[2], min: p[4], median: p[3])).ToArray()) { Name = s.Name }),
    "pie" => new PieChart
    {
        RenderMode = mode,
        Series = spec.Series.Select(s => (ISeries)new PieSeries<double>(s.Values?[0] ?? 0) { Name = s.Name }).ToArray(),
        LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom,
    },
    "polar" => new PolarChart
    {
        RenderMode = mode,
        Series = spec.Series.Select(s => (ISeries)new PolarLineSeries<double>(s.Values ?? []) { Name = s.Name, GeometrySize = 0, LineSmoothness = 0.6 }).ToArray(),
        LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom,
    },
    null  => throw new ArgumentException("missing 'kind'"),
    var k => throw new ArgumentException($"unknown 'kind': {k}"),
};

static InMemoryConsoleChart Cartesian(ConsoleRenderMode mode, ChartSpec spec, Func<SeriesSpec, ISeries> factory) =>
    new CartesianChart
    {
        RenderMode = mode,
        Series = spec.Series.Select(factory).ToArray(),
        XAxes = spec.XAxis is { } xa ? [new Axis { Name = xa.Name, Labels = xa.Labels }] : null!,
        YAxes = spec.YAxis is { } ya ? [new Axis { Name = ya.Name, Labels = ya.Labels }] : null!,
        LegendPosition = spec.Series.Length > 1 ? LiveChartsCore.Measure.LegendPosition.Bottom : LiveChartsCore.Measure.LegendPosition.Hidden,
    };

static void PrintUsage() => System.Console.Error.WriteLine("""
    usage: lvc [--mode auto|sixel|braille|halfblock] [--width N] [--height N] [--json '<spec>' | --file path]

    JSON spec:
      {
        "kind": "line",                       // line | column | row | step | scatter | stackedcolumn | stackedrow | stackedarea | candlestick | box | pie | polar
        "title": "optional",
        "width": 80, "height": 20,            // cells; defaults to terminal size
        "series": [
          {
            "name": "Signal",
            "values": [1, 2, 3, ...],         // line/column/row/step/stacked/pie/polar
            "points": [[x, y], ...]           // scatter / candlestick (open,high,close,low) / box (max,q3,q1,median,min)
          }
        ],
        "xAxis": { "name": "X", "labels": ["a","b","c"] },
        "yAxis": { "name": "Y" }
      }

    Reads from stdin if --json/--file not given:
      echo '{"kind":"line","series":[{"values":[1,2,3,4,3,2,1]}]}' | lvc
    """);

// ----------------------------------------------------------------------------
// JSON model. Records keep the schema legible; STJ source-gen avoids reflection
// at startup so the CLI launches faster.
// ----------------------------------------------------------------------------

internal record ChartSpec(
    string? Kind,
    string? Title,
    int? Width,
    int? Height,
    SeriesSpec[] Series,
    AxisSpec? XAxis,
    AxisSpec? YAxis);

internal record SeriesSpec(
    string? Name,
    double[]? Values,
    double[][]? Points);

internal record AxisSpec(
    string? Name,
    string[]? Labels);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ChartSpec))]
internal partial class ChartJsonContext : JsonSerializerContext { }
