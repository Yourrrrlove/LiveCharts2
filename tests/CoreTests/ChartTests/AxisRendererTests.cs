using LiveChartsCore;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel.Sketches;
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
}
