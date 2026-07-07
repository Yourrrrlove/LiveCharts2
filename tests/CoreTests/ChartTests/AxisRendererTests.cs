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

        public LvcSize Measure(ICartesianAxis axis, Chart chart)
        {
            MeasureCalls++;
            return new LvcSize(10, 20);
        }

        public void Draw(ICartesianAxis axis, Chart chart) => DrawCalls++;
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
}
