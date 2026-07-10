using System;
using LiveChartsCore;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.SKCharts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CoreTests.ChartTests;

[TestClass]
public class AxisRendererTests
{
    // A custom IAxisRenderer assigned to Axis.Renderer must fully replace the built-in axis
    // measure + draw (the seam the backers package plugs its renderers into).
    private sealed class CountingRenderer : IAxisRenderer
    {
        public int MeasureCalls;
        public int DrawCalls;
        public int ClearCalls;

        public LvcSize Measure(ICartesianAxis axis, Chart chart)
        {
            MeasureCalls++;
            return new LvcSize(10, 20);
        }

        public void Draw(ICartesianAxis axis, Chart chart) => DrawCalls++;

        public void Clear(ICartesianAxis axis, Chart chart) => ClearCalls++;
    }

    [TestMethod]
    public void Custom_renderer_replaces_builtin_axis_measure_and_draw()
    {
        var renderer = new CountingRenderer();

        var chart = new SKCartesianChart
        {
            Width = 400,
            Height = 300,
            Series = [new LineSeries<double> { Values = [1, 2, 3] }],
            XAxes = [new Axis { Renderer = renderer }],
            YAxes = [new Axis()],
        };

        _ = chart.GetImage();

        Assert.IsTrue(renderer.MeasureCalls > 0, "the custom renderer's Measure should replace the built-in measure");
        Assert.IsTrue(renderer.DrawCalls > 0, "the custom renderer's Draw should replace the built-in draw");
    }

    // Counts Delete calls — Delete removes the axis' paint tasks from the canvas, so it must only run when
    // the built-in draw was genuinely active in a prior frame, never on an axis that started with a renderer.
    private sealed class DeleteCountingAxis : Axis
    {
        public int DeleteCalls;
        public override void Delete(Chart chart)
        {
            DeleteCalls++;
            base.Delete(chart);
        }
    }

    [TestMethod]
    public void Renderer_set_from_the_start_does_not_tear_down_the_builtin_on_the_first_frame()
    {
        // The axis begins with a Renderer, so the built-in draw is never used — the first Invalidate must not
        // sweep it (there is nothing to sweep, and Delete would strip paint tasks the renderer may share).
        var axis = new DeleteCountingAxis { Renderer = new CountingRenderer() };
        var chart = new SKCartesianChart
        {
            Width = 400,
            Height = 300,
            Series = [new LineSeries<double> { Values = [1, 2, 3] }],
            XAxes = [axis],
            YAxes = [new Axis()],
        };

        _ = chart.GetImage();

        Assert.AreEqual(0, axis.DeleteCalls, "an axis that starts with a renderer must not Delete the built-in draw it never used");
    }

    [TestMethod]
    public void Switching_from_the_builtin_to_a_renderer_tears_down_the_builtin()
    {
        // The axis starts on the built-in draw, then a renderer takes over: now the built-in visuals DO exist
        // and must be swept exactly once so they don't linger beside the renderer's output.
        var axis = new DeleteCountingAxis();
        var chart = new SKCartesianChart
        {
            Width = 400,
            Height = 300,
            Series = [new LineSeries<double> { Values = [1, 2, 3] }],
            XAxes = [axis],
            YAxes = [new Axis()],
        };

        _ = chart.GetImage();
        Assert.AreEqual(0, axis.DeleteCalls, "the built-in draw must not tear itself down while it is in use");

        axis.Renderer = new CountingRenderer();
        _ = chart.GetImage();
        Assert.AreEqual(1, axis.DeleteCalls, "switching from the built-in draw to a renderer must sweep the built-in visuals once");
    }

    [TestMethod]
    public void Removing_the_axis_clears_the_renderer_that_actually_drew()
    {
        // Renderer is reassigned AFTER the last draw but BEFORE the next Invalidate, then the axis is removed.
        // The Invalidate sweep never runs, so RemoveFromUI must clear the renderer that actually painted the
        // last frame (first) — not just the current Renderer — or first's visuals linger on the canvas.
        var first = new CountingRenderer();
        var second = new CountingRenderer();
        var axis = new Axis { Renderer = first };

        var chart = new SKCartesianChart
        {
            Width = 400,
            Height = 300,
            Series = [new LineSeries<double> { Values = [1, 2, 3] }],
            XAxes = [axis],
            YAxes = [new Axis()],
        };

        _ = chart.GetImage();
        Assert.IsTrue(first.DrawCalls > 0, "first renderer should have drawn the last frame");

        // reassign without re-rendering, then remove the axis directly
        axis.Renderer = second;
        var core = (CartesianChartEngine)chart.CoreChart;
        axis.RemoveFromUI(core);

        Assert.AreEqual(1, first.ClearCalls, "the renderer that actually drew must be cleared when the axis is removed");
    }

    [TestMethod]
    public void Swapping_the_renderer_clears_the_previous_one()
    {
        var first = new CountingRenderer();
        var second = new CountingRenderer();
        var axis = new Axis { Renderer = first };

        var chart = new SKCartesianChart
        {
            Width = 400,
            Height = 300,
            Series = [new LineSeries<double> { Values = [1, 2, 3] }],
            XAxes = [axis],
            YAxes = [new Axis()],
        };

        _ = chart.GetImage();
        Assert.AreEqual(0, first.ClearCalls, "the active renderer must not be cleared while it is in use");

        // swap to another renderer: the previous one must be swept
        axis.Renderer = second;
        _ = chart.GetImage();
        Assert.AreEqual(1, first.ClearCalls, "the previous renderer must be cleared when a new one takes over");
        Assert.IsTrue(second.DrawCalls > 0, "the new renderer must draw after the swap");

        // swap back to the built-in draw (e.g. a theme reset): the second renderer must be swept too
        axis.Renderer = null;
        _ = chart.GetImage();
        Assert.AreEqual(1, second.ClearCalls, "removing the renderer must clear it so the built-in axis draws alone");
    }

    // A custom IScalerProvider assigned to Axis.ScalerProvider must be the single source of the axis'
    // coordinate mapping: every scaler the engine builds — series measure, gridlines, and the public
    // ScaleDataToPixels/ScalePixelsToData paths — must flow through it, and the optional Bounds argument
    // must be forwarded untouched.
    private sealed class SpyScalerProvider : IScalerProvider
    {
        public int Calls;
        public Bounds? LastBounds;
        public Chart? LastChart;

        public Scaler GetScaler(ICartesianAxis axis, Chart chart, LvcPoint location, LvcSize size, Bounds? bounds)
        {
            Calls++;
            LastBounds = bounds;
            LastChart = chart;
            // deliberately squash the plot to half its size so the mapping is provably different from the
            // default scaler — if a call site ignored us and used `new Scaler(...)`, the pixel a data value
            // maps to would not match this provider's own scaler.
            return new Scaler(location, new LvcSize(size.Width / 2f, size.Height / 2f), axis, bounds);
        }
    }

    [TestMethod]
    public void Custom_scaler_provider_is_consulted_end_to_end()
    {
        var provider = new SpyScalerProvider();
        var xAxis = new Axis { ScalerProvider = provider };

        var chart = new SKCartesianChart
        {
            Width = 400,
            Height = 300,
            Series = [new LineSeries<double> { Values = [1, 2, 3] }],
            XAxes = [xAxis],
            YAxes = [new Axis()],
        };

        _ = chart.GetImage();

        // the provider was consulted while measuring the chart (series + gridlines build scalers).
        Assert.IsTrue(provider.Calls > 0, "the custom scaler provider must be consulted during measure");

        var engine = (CartesianChartEngine)chart.CoreChart;

        // the public data<->pixel path must map through the provider's scaler, not a default `new Scaler(...)`.
        var expected = provider.GetScaler(xAxis, engine, engine.DrawMarginLocation, engine.DrawMarginSize, null).ToPixels(2d);
        var actual = engine.ScaleDataToPixels(new LvcPointD(2d, 0d)).X;
        Assert.AreEqual(expected, actual, 1e-4, "ScaleDataToPixels must map through the custom provider");
    }

    [TestMethod]
    public void Custom_scaler_provider_receives_the_forwarded_bounds()
    {
        var provider = new SpyScalerProvider();
        var xAxis = new Axis { ScalerProvider = provider };

        var chart = new SKCartesianChart
        {
            Width = 400,
            Height = 300,
            Series = [new LineSeries<double> { Values = [1, 2, 3] }],
            XAxes = [xAxis],
            YAxes = [new Axis()],
        };

        _ = chart.GetImage(); // establishes the axis orientation so a scaler can be built

        var engine = (CartesianChartEngine)chart.CoreChart;
        var bounds = new Bounds();
        _ = xAxis.GetScaler(engine, new LvcPoint(0, 0), new LvcSize(100, 100), bounds);

        Assert.AreSame(bounds, provider.LastBounds, "the optional Bounds argument must be forwarded to the provider untouched");
    }

    // An axis can be shared across charts, so the provider is told which chart it is scaling for. A provider
    // that keys per-chart state on it, or that needs the chart to reach the canvas, depends on this being the
    // measuring chart and not, say, the axis' first chart.
    [TestMethod]
    public void Custom_scaler_provider_receives_the_measuring_chart()
    {
        var provider = new SpyScalerProvider();

        var chart = new SKCartesianChart
        {
            Width = 400,
            Height = 300,
            Series = [new LineSeries<double> { Values = [1, 2, 3] }],
            XAxes = [new Axis { ScalerProvider = provider }],
            YAxes = [new Axis()],
        };

        _ = chart.GetImage();

        Assert.AreSame(
            chart.CoreChart, provider.LastChart,
            "the chart that measured the axis must be the chart handed to the scaler provider");
    }

    // A non-linear (here: logarithmic) X scaler: ToPixels is affine in a WINDOW-INDEPENDENT transform
    // (log of the value), not in the value itself — the class the pixel-space pivot zoom is built for.
    // IsLinear says so; ToPixels/ToChartValues are exact inverses so a value under a pixel survives a zoom.
    private sealed class LogScaler : Scaler
    {
        private readonly double _leftPx, _widthPx, _logMin, _logMax;

        private static double Log(double v) => Math.Log(Math.Max(1e-9, v)); // padding can push the view <= 0

        public LogScaler(LvcPoint loc, LvcSize size, ICartesianAxis axis, Bounds? bounds)
            : base(loc, size, axis, bounds)
        {
            _leftPx = loc.X;
            _widthPx = size.Width;
            _logMin = Log(bounds?.Min ?? axis.MinLimit ?? axis.VisibleDataBounds.Min);
            _logMax = Log(bounds?.Max ?? axis.MaxLimit ?? axis.VisibleDataBounds.Max);
        }

        public override bool IsLinear => false;

        public override float ToPixels(double value) =>
            (float)(_leftPx + _widthPx * (Log(value) - _logMin) / (_logMax - _logMin));

        public override double ToChartValues(double pixels) =>
            Math.Exp(_logMin + (pixels - _leftPx) / _widthPx * (_logMax - _logMin));
    }

    private sealed class LogScalerProvider : IScalerProvider
    {
        public Scaler GetScaler(ICartesianAxis axis, Chart chart, LvcPoint location, LvcSize size, Bounds? bounds) =>
            new LogScaler(location, size, axis, bounds);
    }

    [TestMethod]
    public void Default_scaler_is_linear()
    {
        // The base scaler advertises IsLinear so the pivot zoom keeps its fast, exact value-space path.
        var axis = new Axis();
        var chart = new SKCartesianChart
        {
            Width = 400,
            Height = 300,
            Series = [new LineSeries<double> { Values = [1, 2, 3] }],
            XAxes = [axis],
            YAxes = [new Axis()],
        };
        _ = chart.GetImage();

        Assert.IsTrue(((ICartesianAxis)axis).GetScaler(chart.CoreChart, new LvcPoint(0, 0), new LvcSize(100, 100)).IsLinear);
    }

    [TestMethod]
    public void Zoom_keeps_the_pivot_under_the_pointer_for_a_non_linear_scaler()
    {
        // With a non-linear scaler, value ratios don't equal pixel ratios, so the pivot zoom must scale the
        // window in pixel space. Repeated wheel-zoom at a fixed off-center pixel must keep the value that
        // was under it pinned to that pixel (a value-space ratio would drift it away).
        var xAxis = new Axis { ScalerProvider = new LogScalerProvider() };
        var chart = new SKCartesianChart
        {
            Width = 800,
            Height = 400,
            Series = [new LineSeries<double> { Values = [1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024] }],
            XAxes = [xAxis],
            YAxes = [new Axis()],
        };
        _ = chart.GetImage();

        var engine = (CartesianChartEngine)chart.CoreChart;
        var ax = (ICartesianAxis)xAxis;
        var pivotPx = engine.DrawMarginLocation.X + engine.DrawMarginSize.Width * 0.35f;

        var worst = 0f;
        for (var step = 0; step < 5; step++)
        {
            var pivotValue = ax.GetNextScaler(engine).ToChartValues(pivotPx);
            engine.Zoom(ZoomAndPanMode.X, new LvcPoint(pivotPx, 0), ZoomDirection.ZoomIn);
            _ = chart.GetImage();
            worst = Math.Max(worst, Math.Abs(ax.GetNextScaler(engine).ToPixels(pivotValue) - pivotPx));
        }

        Assert.IsTrue(worst < 1f, $"the pivot must stay under the pointer while zooming a non-linear axis; worst drift was {worst:0.00}px");
    }
}
