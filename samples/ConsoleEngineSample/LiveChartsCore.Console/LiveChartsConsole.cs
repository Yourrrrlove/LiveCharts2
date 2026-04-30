// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using LiveChartsCore.Console.Painting;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.Painting;
using LiveChartsCore.Themes;
using LvcEasings = LiveChartsCore.EasingFunctions;

namespace LiveChartsCore.Console;

public static class LiveChartsConsole
{
    internal static LiveChartsSettings EnsureInitialized()
    {
        LiveCharts.Configure(settings =>
        {
            if (!LiveCharts.DefaultSettings.HasBackedDefined) _ = settings.AddConsole();
            if (!LiveCharts.DefaultSettings.HasThemeDefined) _ = settings.AddConsoleDefaultTheme();
            if (!LiveCharts.DefaultSettings.HasMappersDefined) _ = settings.AddDefaultMappers();
        });
        return LiveCharts.DefaultSettings;
    }

    public static LiveChartsSettings AddConsole(this LiveChartsSettings settings) =>
        settings.HasProvider(new ConsoleProvider());

    public static LiveChartsSettings AddConsoleDefaultTheme(this LiveChartsSettings settings) =>
        settings
            .HasTheme(theme => theme
                .OnInitialized(() =>
                {
                    theme.AnimationsSpeed = TimeSpan.FromMilliseconds(500);
                    theme.EasingFunction = LvcEasings.ExponentialOut;
                    theme.Colors = ColorPalletes.MaterialDesign500;
                    theme.VirtualBackroundColor = new(0, 0, 0);
                })
                .HasDefaultTooltip(() => new ConsoleNoopTooltip())
                .HasDefaultLegend(() => new ConsoleNoopLegend())
                .HasRuleForAxes(axis =>
                {
                    axis.TextSize = 2; // 2 sub-pixels = 1 cell row.
                    axis.ShowSeparatorLines = true;
                    axis.LabelsPaint = new SolidColorPaint(new(180, 180, 180));

                    var lineColor = new LvcColor(80, 80, 80);

                    if (axis is ICartesianAxis cartesian)
                    {
                        axis.SeparatorsPaint = cartesian.Orientation == AxisOrientation.X
                            ? null
                            : new SolidColorPaint(lineColor);

                        cartesian.Padding = new Padding(2);
                    }
                    else
                    {
                        axis.SeparatorsPaint = new SolidColorPaint(lineColor);
                    }
                })
                .HasRuleForLineSeries(line =>
                {
                    var color = theme.GetSeriesColor(line);
                    line.Stroke = new SolidColorPaint(color, 1f);
                    line.Fill = null;
                    line.GeometrySize = 0; // hide point geometries (rectangles look ugly here).
                    line.GeometryStroke = null;
                    line.GeometryFill = null;
                }));
}

internal class ConsoleNoopTooltip : IChartTooltip
{
    public void Show(IEnumerable<ChartPoint> foundPoints, Chart chart) { }
    public void Hide(Chart chart) { }
}

internal class ConsoleNoopLegend : IChartLegend
{
    public void Draw(Chart chart) { }
    public void Hide(Chart chart) { }
    public LvcSize Measure(Chart chart) => new(0, 0);
}
