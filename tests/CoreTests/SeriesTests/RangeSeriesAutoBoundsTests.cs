using System;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Motion;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.SKCharts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CoreTests.SeriesTests;

// Range series default bounds-calc used PrimaryValue alone, so the auto
// value-axis range was [min(High), max(High)] — clipping any bar whose Low
// extended further out (Gantt's first task on Jun 1->Jun 6 vs. min(High)=Jun 6
// elsewhere). Fix merges TertiaryBounds (the Low endpoints) into the value
// axis. These tests pin that contract: GetBounds must report a value-axis
// range that covers both [min(Low), max(High)] regardless of which endpoint
// is the outer extreme.
[TestClass]
public class RangeSeriesAutoBoundsTests
{
    [TestMethod]
    public void RangeRowSeries_AutoBounds_IncludeLowestLowEndpoint()
    {
        // Gantt-shape data: task 0 starts at 0 (the leftmost Low) and ends at
        // 5; task 1 starts at 6 (so min(High) = 5, not 0). Without the fix the
        // X axis would start at 5 and hide the first half of task 0.
        var series = new RangeRowSeries<RangeValue>
        {
            Values =
            [
                new(0, 5),
                new(6, 10),
                new(8, 14),
            ],
        };
        var chart = new SKCartesianChart
        {
            Width = 400,
            Height = 400,
            Series = [series],
            XAxes = [new Axis()],
            YAxes = [new Axis { Labels = ["A", "B", "C"] }],
        };

        var core = (CartesianChartEngine)chart.CoreChart;
        CoreMotionCanvas.DebugElapsedMilliseconds = 0;
        core.IsLoaded = true;
        core._isFirstDraw = true;
        core.Measure();

        try
        {
            var xAxis = core.XAxes[0];
            var yAxis = core.YAxes[0];
            var sb = series.GetBounds(core, yAxis, xAxis);

            Assert.IsFalse(sb.HasData, "expected populated bounds for a non-empty series");

            // HorizontalBar's axis swap means the returned SecondaryBounds is
            // the X-axis (value) range. Without the fix this would be
            // [5, 14] (only High values); with the fix it must include
            // the smallest Low (0) and the largest High (14).
            Assert.IsTrue(sb.Bounds.VisibleSecondaryBounds.Min <= 0,
                $"X auto-min {sb.Bounds.VisibleSecondaryBounds.Min} must include lowest Low (0); " +
                $"missing it means task #0's left edge is clipped off-axis.");
            Assert.IsTrue(sb.Bounds.VisibleSecondaryBounds.Max >= 14,
                $"X auto-max {sb.Bounds.VisibleSecondaryBounds.Max} must include highest High (14).");
        }
        finally
        {
            CoreMotionCanvas.DebugElapsedMilliseconds = -1;
        }
    }

    [TestMethod]
    public void RangeColumnSeries_AutoBounds_IncludeLowestLowEndpoint()
    {
        // Waterfall-shape data where one step's Low is the global minimum and
        // another step's High is below it — without the fix the Y axis would
        // start at min(High) and clip the low-Low bar.
        var series = new RangeColumnSeries<RangeValue>
        {
            Values =
            [
                new(0,  100),   // Low=0 is the global Y minimum
                new(100, 150),
                new(180, 130),  // Low=180 > min(High)=100
            ],
        };
        var chart = new SKCartesianChart
        {
            Width = 400,
            Height = 400,
            Series = [series],
            XAxes = [new Axis { Labels = ["Start", "Sales", "Costs"] }],
            YAxes = [new Axis()],
        };

        var core = (CartesianChartEngine)chart.CoreChart;
        CoreMotionCanvas.DebugElapsedMilliseconds = 0;
        core.IsLoaded = true;
        core._isFirstDraw = true;
        core.Measure();

        try
        {
            var xAxis = core.XAxes[0];
            var yAxis = core.YAxes[0];
            var sb = series.GetBounds(core, xAxis, yAxis);

            Assert.IsFalse(sb.HasData);

            // VerticalBar keeps the unswapped view: PrimaryBounds = Y axis
            // (value). Auto range must cover [min(Low)=0, max(High)=180].
            Assert.IsTrue(sb.Bounds.VisiblePrimaryBounds.Min <= 0,
                $"Y auto-min {sb.Bounds.VisiblePrimaryBounds.Min} must include lowest Low (0).");
            Assert.IsTrue(sb.Bounds.VisiblePrimaryBounds.Max >= 180,
                $"Y auto-max {sb.Bounds.VisiblePrimaryBounds.Max} must include highest Low (180); " +
                $"missing it means the Costs bar's top edge is clipped above the chart.");
        }
        finally
        {
            CoreMotionCanvas.DebugElapsedMilliseconds = -1;
        }
    }

    [TestMethod]
    public void RangeLineSeries_AutoBounds_IncludeLowestLowEndpoint()
    {
        // RangeLine inherits the same GetBounds override as the bar variants:
        // PrimaryValue (High) alone seeds the Y-axis bounds, so without folding
        // TertiaryValue (Low) the auto axis would clip the low curve wherever
        // min(Low) < min(High).
        var series = new RangeLineSeries<RangeValue>
        {
            Values =
            [
                new(0,  100),
                new(50, 150),
                new(75, 130),
            ],
        };
        var chart = new SKCartesianChart
        {
            Width = 400,
            Height = 400,
            Series = [series],
            XAxes = [new Axis()],
            YAxes = [new Axis()],
        };

        var core = (CartesianChartEngine)chart.CoreChart;
        CoreMotionCanvas.DebugElapsedMilliseconds = 0;
        core.IsLoaded = true;
        core._isFirstDraw = true;
        core.Measure();

        try
        {
            var xAxis = core.XAxes[0];
            var yAxis = core.YAxes[0];
            var sb = series.GetBounds(core, xAxis, yAxis);

            Assert.IsFalse(sb.HasData);

            Assert.IsTrue(sb.Bounds.VisiblePrimaryBounds.Min <= 0,
                $"Y auto-min {sb.Bounds.VisiblePrimaryBounds.Min} must include lowest Low (0).");
            Assert.IsTrue(sb.Bounds.VisiblePrimaryBounds.Max >= 150,
                $"Y auto-max {sb.Bounds.VisiblePrimaryBounds.Max} must include highest High (150).");
        }
        finally
        {
            CoreMotionCanvas.DebugElapsedMilliseconds = -1;
        }
    }

    [TestMethod]
    public void RangeColumnSeries_AutoPadding_UsesFullSpanTick()
    {
        // Two series with the SAME full [low, high] span (0..99) but a different High
        // sub-range must auto-fit the value axis identically — the data padding is a
        // function of the whole span, not just the High track. Pre-fix the padding tick
        // came from the High track alone (base.GetBounds runs before Low is merged), so
        // series B (High in [50, 99]) got a finer tick and a tighter margin than series A
        // (High in [0, 99]) despite covering the same range.
        var lowIsOuter = FitValueBounds(new RangeColumnSeries<RangeValue>
        {
            Values = [new(0, 0), new(0, 99)],   // Low=0 const, High in [0, 99] → full [0, 99]
        });
        var highIsOuter = FitValueBounds(new RangeColumnSeries<RangeValue>
        {
            Values = [new(0, 50), new(49, 99)], // Low in [0, 49], High in [50, 99] → full [0, 99]
        });

        Assert.AreEqual(lowIsOuter.min, highIsOuter.min, 1e-9,
            $"value-axis auto-min differs ({lowIsOuter.min} vs {highIsOuter.min}) for the same [0, 99] span; " +
            "the padding tick must reflect the full span, not the High sub-range.");
        Assert.AreEqual(lowIsOuter.max, highIsOuter.max, 1e-9,
            $"value-axis auto-max differs ({lowIsOuter.max} vs {highIsOuter.max}) for the same [0, 99] span.");
    }

    [TestMethod]
    public void RangeLineSeries_AutoPadding_UsesFullSpanTick()
    {
        // Same contract as the column variant — RangeLine folds the Low endpoint into the
        // value axis the same way, so its padding tick must also reflect the full span.
        var lowIsOuter = FitValueBounds(new RangeLineSeries<RangeValue>
        {
            Values = [new(0, 0), new(0, 99)],
        });
        var highIsOuter = FitValueBounds(new RangeLineSeries<RangeValue>
        {
            Values = [new(0, 50), new(49, 99)],
        });

        Assert.AreEqual(lowIsOuter.min, highIsOuter.min, 1e-9,
            $"value-axis auto-min differs ({lowIsOuter.min} vs {highIsOuter.min}) for the same [0, 99] span.");
        Assert.AreEqual(lowIsOuter.max, highIsOuter.max, 1e-9,
            $"value-axis auto-max differs ({lowIsOuter.max} vs {highIsOuter.max}) for the same [0, 99] span.");
    }

    [TestMethod]
    public void RangeRowSeries_AutoPadding_UsesFullSpanTick()
    {
        // Horizontal variant: for RangeRow the value axis is X (secondary), and the same
        // quirk lived there — base.GetBounds resolved the secondary padding tick from the
        // High track before the Low endpoint was merged. Two rows with the SAME full [0, 99]
        // value span but a different High sub-range must resolve the SAME value-axis padding.
        //
        // Unlike the vertical variants this asserts at the GetBounds level (the series'
        // SecondaryBounds padding) rather than the fitted axis: a horizontal auto value axis
        // re-resolves its own tick from the category (Y) axis, which happens to round both
        // sub-ranges to the same nice number and would mask the difference the fix targets.
        var lowIsOuter = RowValuePadding([new(0, 0), new(0, 99)]);   // Low=0 const, High in [0, 99]
        var highIsOuter = RowValuePadding([new(0, 50), new(49, 99)]); // Low in [0, 49], High in [50, 99]

        Assert.AreEqual(lowIsOuter, highIsOuter, 1e-9,
            $"value-axis padding differs ({lowIsOuter} vs {highIsOuter}) for the same [0, 99] span; " +
            "the padding tick must reflect the full span, not the High sub-range.");
    }

    // Measures a RangeRow series and returns the value-axis (X = secondary) data padding
    // the series reports — the quantity the full-span-tick fix corrects.
    private static double RowValuePadding(RangeValue[] values)
    {
        var series = new RangeRowSeries<RangeValue> { Values = values };
        var chart = new SKCartesianChart
        {
            Width = 1000,
            Height = 500,
            Series = [series],
            XAxes = [new Axis()],
            YAxes = [new Axis()],
        };
        var core = (CartesianChartEngine)chart.CoreChart;
        CoreMotionCanvas.DebugElapsedMilliseconds = 0;
        core.IsLoaded = true;
        core._isFirstDraw = true;
        core.Measure();
        try
        {
            // HorizontalBar swaps the axes: the value axis is the primary (X) argument, and
            // its bounds land in SecondaryBounds — where the padding recompute applies.
            var sb = series.GetBounds(core, core.YAxes[0], core.XAxes[0]);
            return sb.Bounds.SecondaryBounds.PaddingMax;
        }
        finally
        {
            CoreMotionCanvas.DebugElapsedMilliseconds = -1;
        }
    }

    // Fits a vertical range series and returns the padded value-axis (Y) bounds.
    private static (double min, double max) FitValueBounds(ISeries series)
    {
        var yAxis = new Axis();
        var chart = new SKCartesianChart
        {
            Width = 1000,
            Height = 500,
            Series = [series],
            XAxes = [new Axis()],
            YAxes = [yAxis],
        };
        using var _ = chart.GetImage();
        return (yAxis.VisibleDataBounds.Min, yAxis.VisibleDataBounds.Max);
    }

    [TestMethod]
    public void RangeRowSeries_DateTimeAxis_AutoMinReachesEarliestStart()
    {
        // The actual Gantt repro: project starts Jun 1, first task spans
        // Jun 1->Jun 6, every other task starts later. Pre-fix the X axis
        // auto-min would have been Jun 6 (min of all High ticks), clipping
        // the first task. This test pins the "first task is fully drawn"
        // contract under a DateTime axis specifically.
        var projectStart = new DateTime(2026, 6, 1);
        var firstTaskStartTicks = projectStart.Ticks;
        var series = new RangeRowSeries<RangeValue>
        {
            Values =
            [
                new(projectStart.AddDays(0).Ticks,  projectStart.AddDays(5).Ticks),
                new(projectStart.AddDays(3).Ticks,  projectStart.AddDays(8).Ticks),
                new(projectStart.AddDays(14).Ticks, projectStart.AddDays(20).Ticks),
            ],
        };
        var chart = new SKCartesianChart
        {
            Width = 400,
            Height = 400,
            Series = [series],
            XAxes = [new DateTimeAxis(TimeSpan.FromDays(2), d => d.ToString("MMM dd"))],
            YAxes = [new Axis { Labels = ["A", "B", "C"] }],
        };

        var core = (CartesianChartEngine)chart.CoreChart;
        CoreMotionCanvas.DebugElapsedMilliseconds = 0;
        core.IsLoaded = true;
        core._isFirstDraw = true;
        core.Measure();

        try
        {
            var xAxis = core.XAxes[0];
            var yAxis = core.YAxes[0];
            var sb = series.GetBounds(core, yAxis, xAxis);

            Assert.IsTrue(sb.Bounds.VisibleSecondaryBounds.Min <= firstTaskStartTicks,
                $"X auto-min {new DateTime((long)sb.Bounds.VisibleSecondaryBounds.Min):MMM dd} " +
                $"must include project start ({projectStart:MMM dd}); without it the first task is clipped.");
        }
        finally
        {
            CoreMotionCanvas.DebugElapsedMilliseconds = -1;
        }
    }
}
