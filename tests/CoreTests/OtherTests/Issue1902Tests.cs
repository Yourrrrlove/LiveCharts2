// The MIT License(MIT)
//
// Copyright(c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.SKCharts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;

namespace CoreTests.OtherTests;

// Issue #1902: a paint set on a single point's label did nothing, the label kept the series'
// DataLabelsPaint. A series assigns label.Paint = DataLabelsPaint on every measure, so a per-point
// paint can only be set after that, from the PointMeasured hook -- and it then has to win.
//
// It wins because a label reports its own Paint as its Fill: when the series' DataLabelsPaint task
// draws the label, the draw path reads the element's Fill and paints with that instead of with the
// active task. That is the whole reason BaseLabelGeometry aliases Fill onto Paint, so this test is
// what stops the alias from being "cleaned up" as pointless.
[TestClass]
public class Issue1902Tests
{
    [TestMethod]
    public void APaintOnASinglePointsLabelWinsOverTheSeriesDataLabelsPaint()
    {
        var series = new ColumnSeries<int>
        {
            Values = [1, 2, 3],
            DataLabelsPaint = new SolidColorPaint(SKColors.Blue),
            DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Middle,
            DataLabelsSize = 30
        };

        // Repaint the middle point's label, exactly as the issue asks for.
        series.PointMeasured += point =>
        {
            if (point.Index != 1) return;
            point.Context.Label!.Paint = new SolidColorPaint(SKColors.Red);
        };

        var chart = new SKCartesianChart
        {
            Width = 400,
            Height = 300,
            Background = SKColors.White,
            Series = [series]
        };

        using var image = chart.GetImage();
        using var data = image.Encode();
        using var bmp = SKBitmap.Decode(data.ToArray());

        var reds = 0;
        var blues = 0;
        for (var x = 0; x < bmp.Width; x++)
            for (var y = 0; y < bmp.Height; y++)
            {
                var c = bmp.GetPixel(x, y);
                if (c.Red > 120 && c.Blue < 80 && c.Green < 80) reds++;
                if (c.Blue > 120 && c.Red < 80 && c.Green < 80) blues++;
            }

        Assert.IsTrue(
            blues > 0,
            "the other labels must still use the series DataLabelsPaint, otherwise this test proves nothing");

        Assert.IsTrue(
            reds > 0,
            "A paint set on a single point's label must be drawn, it must not fall back to the " +
            "series DataLabelsPaint (#1902).");
    }
}
