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
                    // TextSize is informational here — the axis layout reserves space using the
                    // LabelGeometry size which is driven by the render-mode glyph dimensions.
                    axis.TextSize = Drawing.Geometries.LabelGeometry.GlyphPixelsH;
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
                    line.GeometrySize = 0; // hide default point geometries.
                    line.GeometryStroke = null;
                    line.GeometryFill = null;
                })
                .HasRuleForStepLineSeries(step =>
                {
                    var color = theme.GetSeriesColor(step);
                    step.Stroke = new SolidColorPaint(color, 1f);
                    step.Fill = null;
                    step.GeometrySize = 0;
                    step.GeometryStroke = null;
                    step.GeometryFill = null;
                })
                .HasRuleForStackedLineSeries(stacked =>
                {
                    // The base line rule nulls Fill — for stacked area we want the band visible.
                    // ILineSeries is a sibling of IStackedLineSeries (per the SeriesProperties
                    // bitmask check in Theme.cs), so this rule fires after HasRuleForLineSeries
                    // and overwrites just the Fill assignment.
                    var color = theme.GetSeriesColor(stacked);
                    stacked.Fill = new SolidColorPaint(color);
                })
                .HasRuleForStackedStepLineSeries(stacked =>
                {
                    var color = theme.GetSeriesColor(stacked);
                    stacked.Fill = new SolidColorPaint(color);
                })
                .HasRuleForBarSeries(bar =>
                {
                    var color = theme.GetSeriesColor(bar);
                    bar.Stroke = null;
                    bar.Fill = new SolidColorPaint(color);
                    bar.Rx = 1;
                    bar.Ry = 1;
                })
                .HasRuleForStackedBarSeries(stacked =>
                {
                    // IStackedBarSeries is a sibling of IBarSeries, not a child — without this
                    // rule stacked column/row series get no Stroke/Fill set and render nothing.
                    var color = theme.GetSeriesColor(stacked);
                    stacked.Stroke = null;
                    stacked.Fill = new SolidColorPaint(color);
                    stacked.Rx = 0;
                    stacked.Ry = 0;
                })
                .HasRuleForScatterSeries(scatter =>
                {
                    var color = theme.GetSeriesColor(scatter);
                    scatter.Stroke = null;
                    scatter.Fill = new SolidColorPaint(color);
                    scatter.GeometrySize = 8;
                })
                .HasRuleForFinancialSeries(financial =>
                {
                    // Default candle colors don't matter much here — the series toggles UpFill/
                    // DownFill internally based on (open, close). We just provide them.
                    var color = theme.GetSeriesColor(financial);
                    financial.UpFill = new SolidColorPaint(new(120, 220, 120));
                    financial.UpStroke = new SolidColorPaint(new(120, 220, 120), 1f);
                    financial.DownFill = new SolidColorPaint(new(220, 120, 120));
                    financial.DownStroke = new SolidColorPaint(new(220, 120, 120), 1f);
                })
                .HasRuleForBoxSeries(box =>
                {
                    var color = theme.GetSeriesColor(box);
                    box.Stroke = new SolidColorPaint(color, 1f);
                    box.Fill = null;
                })
                .HasRuleForPieSeries(pie =>
                {
                    var color = theme.GetSeriesColor(pie);
                    pie.Stroke = null;
                    pie.Fill = new SolidColorPaint(color);
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
