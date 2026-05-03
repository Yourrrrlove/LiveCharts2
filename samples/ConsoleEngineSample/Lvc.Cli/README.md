# lvc — LiveCharts in your terminal

Render LiveCharts charts in the terminal from a JSON spec. Designed for
shell pipelines and AI-driven clients (Claude Code, MCP-aware agents,
anything that can shell out) that want animated, interactive-quality
charts without integrating the .NET library directly.

> **Status:** experimental. The console rendering backend is part of the
> LiveCharts2 console-renderer experiment and the JSON schema may shift
> before 1.0.

## Install

```sh
dotnet tool install -g LiveChartsCore.Console.Cli
```

Requires the .NET 8 runtime. After install, `lvc` is on your PATH.

## Usage

```sh
echo '{"kind":"line","series":[{"values":[1,2,3,4,3,2,1]}]}' | lvc

lvc --json '{"kind":"pie","title":"Slices","series":[{"name":"A","values":[1]},{"name":"B","values":[3]}]}'

lvc --file chart.json --mode sixel
```

Render mode auto-detects: Sixel if the terminal advertises it via DA1
(modern xterm, WezTerm, foot, mintty 3+, mlterm, Black Box, Konsole 25.04+,
Windows Terminal 1.22+), otherwise Braille — which renders on any UTF-8
terminal. Override with `--mode halfblock|braille|sixel|auto`.

Width / height in cells default to the current terminal; override with
`--width N` / `--height N` (or set them in the JSON spec).

## JSON spec

```json
{
  "kind": "line",
  "title": "optional",
  "width": 80,
  "height": 20,
  "series": [
    {
      "name": "Signal",
      "values": [1, 2, 3, 4, 3, 2, 1],
      "points": [[0, 1], [1, 2]]
    }
  ],
  "xAxis": { "name": "X", "labels": ["a", "b", "c"] },
  "yAxis": { "name": "Y" }
}
```

`kind` accepts: `line`, `column`, `row`, `step`, `scatter`,
`stackedcolumn`, `stackedrow`, `stackedarea`, `candlestick`, `box`,
`pie`, `polar`.

`values` is used by line / column / row / step / stacked / pie / polar
series; `points` is used by scatter (`[x, y]`), candlestick
(`[open, high, close, low]`), and box (`[max, q3, q1, median, min]`).

## License

MIT. See the [LiveCharts2 repo](https://github.com/Live-Charts/LiveCharts2).
