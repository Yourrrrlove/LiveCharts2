using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Drawing;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.SKCharts;
using LiveChartsCore.VisualStates;
using SkiaSharp;
using ViewModelsSamples.General.VisualElements;

namespace SnapshotTests;

[TestClass]
public sealed class SpecialCasesTests
{
    [TestMethod]
    public void NullPoints()
    {
        var chart = new SKCartesianChart
        {
            Series = [
                new LineSeries<int?> { Values = [1, 6, 5, null, 3, 2, 5] }
            ],
            Width = 600,
            Height = 600
        };

        chart.AssertSnapshotMatches($"{nameof(SpecialCasesTests)}_{nameof(NullPoints)}");
    }

    [TestMethod]
    public void CustomPoints()
    {
        var values1 = new double[] { 2, 1, 4 };
        var values2 = new double[] { 4, 3, 6 };
        var values3 = new double[] { -2, 2, 1 };
        var values4 = new double[] { 1, 2, 3 };
        var starPath = SVGPoints.Star;

        var chart = new SKCartesianChart
        {
            Series = [
                new ColumnSeries<double> { Values = values1 },
                new ColumnSeries<double, DiamondGeometry> { Values = values2 },
                new ColumnSeries<double, VariableSVGPathGeometry> { Values = values3, GeometrySvg = starPath },
                new ColumnSeries<double, MyGeometry> { Values = values4 }
            ],
            Width = 600,
            Height = 600
        };

        chart.AssertSnapshotMatches($"{nameof(SpecialCasesTests)}_{nameof(CustomPoints)}");
    }

    [TestMethod]
    public void DrawMarginFrameFillBehindSeries()
    {
        // Regression for https://github.com/Live-Charts/LiveCharts2/issues/1978
        // Opaque DrawMarginFrame.Fill must render behind the series, not on top of it.
        var chart = new SKCartesianChart
        {
            Series = [
                new LineSeries<double>
                {
                    Values = [1, 6, 5, 3, 8, 2, 9],
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColors.Black, 4),
                    GeometryFill = new SolidColorPaint(SKColors.Black),
                    GeometryStroke = new SolidColorPaint(SKColors.Black, 4)
                }
            ],
            DrawMarginFrame = new DrawMarginFrame
            {
                Fill = new SolidColorPaint(SKColors.Red),
                Stroke = new SolidColorPaint(SKColors.DarkRed, 2)
            },
            Width = 600,
            Height = 600
        };

        chart.AssertSnapshotMatches($"{nameof(SpecialCasesTests)}_{nameof(DrawMarginFrameFillBehindSeries)}");
    }

    [TestMethod]
    public void Sections()
    {
        var values = new ObservablePoint[]
        {
            new(2.2, 5.4),
            new(4.5, 2.5),
            new(4.2, 7.4),
            new(6.4, 9.9),
            new(8.9, 3.9),
            new(9.9, 5.2)
        };

        var sections = new RectangularSection[]
        {
            // Section from 3 to 4 in X axis
            new() {
                Xi = 3,
                Xj = 4,
                Fill = new SolidColorPaint(SKColor.Parse("#FFCDD2"))
            },
            // Section from 5 to 6 in X axis and 2 to 8 in Y axis
            new() {
                Xi = 5,
                Xj = 6,
                Yi = 2,
                Yj = 8,
                Fill = new SolidColorPaint(SKColor.Parse("#BBDEFB"))
            },
            // Section from 8 to end in X axis
            new() {
                Xi = 8,
                Label = "A section here!",
                LabelSize = 14,
                LabelPaint = new SolidColorPaint(SKColor.Parse("#FF6F00")),
                Fill = new SolidColorPaint(SKColor.Parse("#F9FBE7"))
            }
        };

        var chart = new SKCartesianChart
        {
            Series = [
                new ScatterSeries<ObservablePoint> { Values = values }
            ],
            Sections = sections,
            Width = 600,
            Height = 600
        };

        chart.AssertSnapshotMatches($"{nameof(SpecialCasesTests)}_{nameof(Sections)}");
    }

    [TestMethod]
    public void Visibility()
    {
        var values0 = new double[] { 2, 5, 4 };
        var values1 = new double[] { 1, 2, 3 };
        var values2 = new double[] { 4, 3, 2 };

        ISeries[] series = [
            new ColumnSeries<double> { Values = values0 },
            new ColumnSeries<double> { Values = values1 },
            new ColumnSeries<double> { Values = values2 }
        ];

        var chart = new SKCartesianChart
        {
            Series = series,
            Width = 600,
            Height = 600
        };


        chart.AssertSnapshotMatches($"{nameof(SpecialCasesTests)}_{nameof(Visibility)}_0");

        series[1].IsVisible = false;
        chart.AssertSnapshotMatches($"{nameof(SpecialCasesTests)}_{nameof(Visibility)}_1");

        series[1].IsVisible = true;
        chart.AssertSnapshotMatches($"{nameof(SpecialCasesTests)}_{nameof(Visibility)}_2");
    }

    [TestMethod]
    public void VisualElements()
    {
        var chart = new SKCartesianChart
        {
            VisualElements = [
                new RectangleVisual(),
                new ScaledRectangleVisual(),
                new PointerDownAwareVisual(),
                new SvgVisual(),
                new ThemedVisual(),
                new CustomVisual(),
                new AbsoluteVisual(),
                new StackedVisual(),
                new TableVisual(),
                new ContainerVisual()
            ],
            Width = 600,
            Height = 600
        };

        chart.AssertSnapshotMatches($"{nameof(SpecialCasesTests)}_{nameof(VisualElements)}");
    }

    [TestMethod]
    public void States()
    {
        var values = new ObservableCollection<ObservableValue>
        {
            new(2), new(3), new(4)
        };

        var columnSeries = new ColumnSeries<ObservableValue>
        {
            ShowDataLabels = true,
            DataLabelsSize = 15,
            Values = values
        };

        // define the danger state, a red fill.
        _ = columnSeries.HasState("Danger", [
            (nameof(IDrawnElement.Fill), new SolidColorPaint(SKColors.Yellow))
        ]);

        _ = columnSeries.HasState("LabelDanger", [
            (nameof(IDrawnElement.Paint), new SolidColorPaint(SKColors.Yellow)),
            (nameof(BaseLabelGeometry.TextSize), 30f),
        ]);

        columnSeries.PointMeasured += point =>
        {
            var ctx = point.Context;
            if (ctx.DataSource is not ObservableValue observable) return;

            var states = ctx.Series.VisualStates;

            if (observable.Value > 5)
            {
                states.SetState("Danger", ctx.Visual);
                states.SetState("LabelDanger", ctx.Label);
            }
            else
            {
                states.ClearState("Danger", ctx.Visual);
                states.ClearState("LabelDanger", ctx.Label);
            }
        };

        var chart = new SKCartesianChart
        {
            Series = [
                columnSeries
            ],
            Width = 600,
            Height = 600
        };

        chart.AssertSnapshotMatches($"{nameof(SpecialCasesTests)}_{nameof(States)}_0");

        values[1].Value = 10;
        chart.AssertSnapshotMatches($"{nameof(SpecialCasesTests)}_{nameof(States)}_1");

        values[1].Value = 1;
        chart.AssertSnapshotMatches($"{nameof(SpecialCasesTests)}_{nameof(States)}_2");

        values[1].Value = 10;
        chart.AssertSnapshotMatches($"{nameof(SpecialCasesTests)}_{nameof(States)}_3");
    }

    private class MyGeometry : BoundedDrawnGeometry, IDrawnElement<SkiaSharpDrawingContext>
    {
        public void Draw(SkiaSharpDrawingContext context)
        {
            var paint = context.ActiveSkiaPaint;
            var canvas = context.Canvas;
            var y = Y;

            while (y < Y + Height)
            {
                canvas.DrawLine(X, y, X + Width, y, paint);
                y += 5;
            }
        }
    }
}
