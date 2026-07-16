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

using LiveChartsCore;
using LiveChartsCore.Painting;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.SKCharts;
using LiveChartsCore.SkiaSharpView.VisualElements;
using LiveChartsCore.Themes;
using LiveChartsCore.VisualElements;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;

namespace CoreTests.OtherTests;

// Covers the chart title following the theme.
//
// DrawnLabelVisual derives from Visual, which had no theme pass at all: ApplyTheme only
// existed on VisualElement and was only ever reached from BaseLabelVisual (the obsolete
// label family). So a title never got a paint from the theme, and since a label can not be
// measured without one, every sample had to hardcode a color that then ignored dark mode.
[TestClass]
public class DrawnLabelVisualThemeTests
{
    private static readonly SKColor s_lightPaint = new(30, 30, 30);
    private static readonly SKColor s_darkPaint = new(200, 200, 200);

    private static SKCartesianChart BuildChart(DrawnLabelVisual title) =>
        new()
        {
            Width = 300,
            Height = 300,
            Title = title,
            Series = [new LineSeries<double> { Values = [1, 2, 3] }]
        };

    private static DrawnLabelVisual BuildTitle() =>
        new(new LabelGeometry { Text = "My chart title", TextSize = 25 });

    private static SKColor ColorOf(Paint? paint) =>
        ((SolidColorPaint)paint!).Color;

    private static void UseTheme(LvcThemeKind kind) =>
        LiveCharts.Configure(s => s.AddDefaultTheme(requestedTheme: kind));

    private static void ResetTheme() =>
        LiveCharts.Configure(s => s.AddDefaultTheme());

    [TestMethod]
    public void TitleWithNoPaintIsThemed()
    {
        // Before the fix this threw "A paint is required to measure a label": MeasureTitle
        // reads the title's size before anything could have given it a paint.
        try
        {
            UseTheme(LvcThemeKind.Light);

            var title = BuildTitle();
            _ = BuildChart(title).GetImage();

            Assert.AreEqual(
                s_lightPaint, ColorOf(title.Paint),
                "A title with no paint must take the paint from the theme.");
        }
        finally
        {
            ResetTheme();
        }
    }

    [TestMethod]
    public void ThemedTitlePaintReachesTheGeometry()
    {
        // The theme sets DrawnLabelVisual.Paint, but the label is drawn from the geometry's
        // own paint, so Measure must forward it or the title renders as nothing.
        try
        {
            UseTheme(LvcThemeKind.Light);

            var title = BuildTitle();
            _ = BuildChart(title).GetImage();

            Assert.AreEqual(
                s_lightPaint, ColorOf(title.Label.Paint),
                "The themed paint must be forwarded to the label geometry.");
        }
        finally
        {
            ResetTheme();
        }
    }

    [TestMethod]
    public void UserSetTitlePaintWinsOverTheme()
    {
        // The whole reason DrawnLabelVisual.Paint exists: a paint set through the property is
        // recorded in _userSets, so CanSetProperty blocks the theme. Writing straight to
        // Label.Paint can not be seen, which is why the theme would overwrite it.
        try
        {
            UseTheme(LvcThemeKind.Light);

            var userPaint = new SolidColorPaint(SKColors.Red);
            var title = BuildTitle();
            title.Paint = userPaint;

            _ = BuildChart(title).GetImage();

            Assert.AreEqual(
                SKColors.Red, ColorOf(title.Paint),
                "A user-set paint must survive the theme.");
            Assert.AreEqual(
                SKColors.Red, ColorOf(title.Label.Paint),
                "A user-set paint must reach the geometry.");
        }
        finally
        {
            ResetTheme();
        }
    }

    [TestMethod]
    public void PaintOnTheCtorGeometryWinsOverTheme()
    {
        // The code-only samples build the title as DrawnLabelVisual(new LabelGeometry { Paint }).
        // That write never went through the Paint property, so the ctor has to route it.
        try
        {
            UseTheme(LvcThemeKind.Light);

            var title = new DrawnLabelVisual(
                new LabelGeometry
                {
                    Text = "My chart title",
                    TextSize = 25,
                    Paint = new SolidColorPaint(SKColors.Red)
                });

            _ = BuildChart(title).GetImage();

            Assert.AreEqual(
                SKColors.Red, ColorOf(title.Label.Paint),
                "A paint handed to the ctor on the geometry must survive the theme.");
        }
        finally
        {
            ResetTheme();
        }
    }

    [TestMethod]
    public void UnsetTitlePaintFollowsAThemeChange()
    {
        // The point of theming the title: switching light <-> dark must repaint it. A rule that
        // only filled in a null paint would pass the first-load test and fail this one.
        try
        {
            UseTheme(LvcThemeKind.Light);

            var title = BuildTitle();
            _ = BuildChart(title).GetImage();

            Assert.AreEqual(s_lightPaint, ColorOf(title.Paint));

            UseTheme(LvcThemeKind.Dark);
            _ = BuildChart(title).GetImage();

            Assert.AreEqual(
                s_darkPaint, ColorOf(title.Paint),
                "An unset title paint must follow a theme change.");
        }
        finally
        {
            ResetTheme();
        }
    }

    [TestMethod]
    public void UserSetTitlePaintSurvivesAThemeChange()
    {
        try
        {
            UseTheme(LvcThemeKind.Light);

            var title = BuildTitle();
            title.Paint = new SolidColorPaint(SKColors.Red);
            _ = BuildChart(title).GetImage();

            UseTheme(LvcThemeKind.Dark);
            _ = BuildChart(title).GetImage();

            Assert.AreEqual(
                SKColors.Red, ColorOf(title.Paint),
                "A user-set paint must survive a theme change too.");
        }
        finally
        {
            ResetTheme();
        }
    }

    [TestMethod]
    public void TitleIsThemedWhenThePolarChartFitsToBounds()
    {
        // PolarChartEngine skips MeasureTitle when it fits to bounds, so AddTitleToChart is the
        // first thing to touch the title on that path and has to measure it too.
        try
        {
            UseTheme(LvcThemeKind.Light);

            var title = BuildTitle();
            var chart = new SKPolarChart
            {
                Width = 300,
                Height = 300,
                Title = title,
                FitToBounds = true,
                Series = [new PolarLineSeries<double> { Values = [1, 2, 3] }]
            };

            _ = chart.GetImage();

            Assert.AreEqual(
                s_lightPaint, ColorOf(title.Paint),
                "The title must be themed on the fit-to-bounds polar path.");
        }
        finally
        {
            ResetTheme();
        }
    }
}
