using System.Linq;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.SKCharts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CoreTests.OtherTests;

// ChartPoint.GetDistanceTo must return the pixel-space distance from the point to a UI location
// (its documented contract). Two latent bugs broke that: it matched Context (a ChartPointContext)
// against ICartesianChartView instead of Context.Chart, so it always threw NotImplementedException;
// and it subtracted ScalePixelsToData(location) (chart values) from the point's pixel coordinates,
// mixing units. These tests pin the corrected pixel-distance behavior.
[TestClass]
public class GetDistanceToTests
{
    [TestMethod]
    public void Distance_to_the_points_own_pixel_is_zero()
    {
        var series = new ScatterSeries<ObservablePoint> { Values = [new(2, 3), new(8, 7)] };
        var chart = new SKCartesianChart
        {
            Width = 500,
            Height = 500,
            Series = [series],
            XAxes = [new Axis()],
            YAxes = [new Axis()],
        };
        _ = chart.GetImage(); // measure so points + scalers exist

        var point = ((ISeries)series).Fetch(chart.CoreChart).First();
        var coord = point.Coordinate;

        // the point's own pixel, computed independently of GetDistanceTo's internals.
        var px = chart.ScaleDataToPixels(new LvcPointD(coord.SecondaryValue, coord.PrimaryValue));
        var ownPixel = new LvcPoint((float)px.X, (float)px.Y);

        Assert.AreEqual(0d, point.GetDistanceTo(ownPixel), 1e-2,
            "distance from a point to its own pixel must be ~0 (mixing data values with pixels made it large).");
    }

    [TestMethod]
    public void Distance_is_the_pixel_space_magnitude()
    {
        var series = new ScatterSeries<ObservablePoint> { Values = [new(2, 3), new(8, 7)] };
        var chart = new SKCartesianChart
        {
            Width = 500,
            Height = 500,
            Series = [series],
            XAxes = [new Axis()],
            YAxes = [new Axis()],
        };
        _ = chart.GetImage();

        var point = ((ISeries)series).Fetch(chart.CoreChart).First();
        var coord = point.Coordinate;
        var px = chart.ScaleDataToPixels(new LvcPointD(coord.SecondaryValue, coord.PrimaryValue));

        // offset the pointer a known number of PIXELS from the point: distance is the 3-4-5 magnitude.
        var offset = new LvcPoint((float)px.X + 30f, (float)px.Y + 40f);

        Assert.AreEqual(50d, point.GetDistanceTo(offset), 1e-2,
            "distance must be measured in pixels — a (30, 40) pixel offset is 50px away.");
    }
}
