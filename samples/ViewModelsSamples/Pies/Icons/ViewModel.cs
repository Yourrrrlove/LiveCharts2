﻿using LiveChartsCore;
using LiveChartsCore.ConditionalDraw;
using LiveChartsCore.Drawing;
using LiveChartsCore.Motion;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Extensions;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.Generic;

namespace ViewModelsSamples.Pies.Icons;

public class ViewModel
{
    public IEnumerable<ISeries> Series { get; set; }

    public ViewModel()
    {
        BrowserShare[] data = [
            new() { Name = "Chrome", Value = 65.72, Svg = Icons.Chrome },
            new() { Name = "Safari", Value = 18.22, Svg = Icons.Safari },
            new() { Name = "Edge", Value = 5.31, Svg = Icons.Edge }
        ];

        // lets create a pie series collection that plots the BrowserShare class,
        // it will use the DoughnutGeometry to draw each point (the default geometry),
        // and uses the CustomDoughnutGeometry to draw each point.
        Series = data.AsPieSeries<BrowserShare, DoughnutGeometry, SvgIconLabel>(
            (data, series) =>
            {
                // lets define the data labels paint.
                series.DataLabelsPaint = new SolidColorPaint(SKColors.WhiteSmoke);

                // now, when the point is measured, we will set up the svg label based on the BrowserShare data.
                series
                    .OnPointMeasured(point =>
                    {
                        var svgLabel = point.Label!;

                        svgLabel.Path = data.Svg;
                        svgLabel.Name = data.Name;
                        svgLabel.Width = 50;
                        svgLabel.Height = 50;
                        svgLabel.TranslateTransform = new(-25, -25);
                    });
            });

        // Lets teach LiveCharts how to handle the BrowserShare class
        // you can learn more about mappings here:
        // // https://livecharts.dev/docs/{{ platform }}/{{ version }}/Overview.Mappers
        LiveCharts.Configure(config => config
            .HasMap<BrowserShare>((point, index) => new(index, point.Value)));
    }
}

public class BrowserShare
{
    public string Name { get; set; }
    public SKPath Svg { get; set; }
    public double Value { get; set; }
}

// this is the geometry that will be used to draw the labels on each point.
// internaly is simplifies a lot the SkiaSharp API and animates everything.
// we inherit from BaseSVGPathGeometry to handle the svg path
// and implement ILabelGeometry to satisfy the series requirements.
public class SvgIconLabel : VariableSVGPathGeometry, ILabelGeometry<SkiaSharpDrawingContext>
{
    public string Name { get; set; }

    public override void OnDraw(SkiaSharpDrawingContext context, SKPaint paint)
    {
        base.OnDraw(context, paint);

        using var textPaint = new SKPaint
        {
            Color = SKColors.WhiteSmoke,
            Style = SKPaintStyle.Fill,
            TextSize = 16,
            FakeBoldText = true,
            IsAntialias = true
        };

        context.Canvas.DrawText(Name, X, Y - 10, textPaint);
    }

    // All the folowing properties and ctor are required by the ILabelGeometry interface
    // in this case we will ignore them.

    public SvgIconLabel()
    {
        // just a workaround... probably will be imprived in a future version.
        _ = RegisterMotionProperty(new FloatMotionProperty(nameof(TextSize), 11));
    }

    public Padding Padding { get; set; } = new();
    public float LineHeight { get; set; }
    public Align VerticalAlign { get; set; }
    public Align HorizontalAlign { get; set; }
    public string Text { get; set; } = string.Empty;
    public float TextSize { get; set; }
    public float MaxWidth { get; set; }
    public LvcColor Background { get; set; }
}

public static class Icons
{
    // from https://www.svgrepo.com/svg/500807/chrome
    public static SKPath Chrome { get; } = SKPath.ParseSvgPathData(
        "M21.25 11.031h-10.063c-0.969 0-1.844 0.313-2.625 0.781-1.344 0.844-2.25 2.344-2.313 " +
        "4.031l-3.875-6.719c2.063-2.625 5.219-4.344 8.813-4.344 4.406 0 8.25 2.563 10.063 " +
        "6.25zM1.875 9.75l5 8.688c0.031 0.031 0.031 0.063 0.063 0.094 0.438 0.781 1.125 " +
        "1.406 1.938 1.844 0.719 0.344 1.469 0.594 2.313 0.594s1.625-0.25 2.344-0.594l-3.906 " +
        "6.719c-5.438-0.781-9.625-5.438-9.625-11.094 0-2.313 0.688-4.469 1.875-6.25zM13.813 " +
        "11.813h7.781c0.5 1.281 0.813 2.719 0.813 4.188 0 6.188-5.031 11.188-11.219 11.188-0.25 " +
        "0-0.469 0-0.719-0.031l5-8.625c0.031-0.031 0.031-0.063 0.063-0.094 0.406-0.75 0.625-1.563 " +
        "0.625-2.438v-0.156c-0.063-1.688-1-3.188-2.344-4.031zM7.469 16c0-2.094 1.625-3.75 3.719-3.75s3.75 " +
        "1.656 3.75 3.75-1.656 3.75-3.75 3.75-3.719-1.656-3.719-3.75z");

    // from: https://www.svgrepo.com/svg/316925/safari
    public static SKPath Safari { get; } = SKPath.ParseSvgPathData(
        "M12 3.5C7.30558 3.5 3.5 7.30558 3.5 12C3.5 13.5799 3.93054 15.0576 4.68039 16.324C4.82109 16.5617 " +
        "4.74251 16.8683 4.5049 17.009C4.26729 17.1497 3.96061 17.0712 3.81992 16.8335C2.98125 15.4171 2.5 " +
        "13.764 2.5 12C2.5 6.75329 6.75329 2.5 12 2.5C17.2467 2.5 21.5 6.75329 21.5 12C21.5 17.2467 17.2467 " +
        "21.5 12 21.5C9.41171 21.5 7.06433 20.4642 5.3514 18.7857C5.24102 18.6776 5.18686 18.5244 5.20469 " +
        "18.3709C5.22251 18.2174 5.31033 18.0808 5.44256 18.0008L13.111 13.3621L16.0946 8.3816L10.9439 " +
        "11.1098L7.81033 15.6802C7.65418 15.908 7.34296 15.966 7.11521 15.8098C6.88746 15.6537 6.82941 15.3425 " +
        "6.98557 15.1147L10.1887 10.4428C10.2344 10.3762 10.2956 10.3216 10.367 10.2837L17.2108 6.65874C17.4091 " +
        "6.5537 17.6531 6.59365 17.8076 6.75646C17.962 6.91926 17.9891 7.16502 17.8737 7.35754L13.905 " +
        "13.9825C13.8631 14.0525 13.8047 14.1112 13.7349 14.1534L6.5337 18.5095C8.01177 19.7521 9.91814 " +
        "20.5 12 20.5C16.6944 20.5 20.5 16.6944 20.5 12C20.5 7.30558 16.6944 3.5 12 3.5Z");

    // from: https://www.svgrepo.com/svg/452194/edge
    public static SKPath Edge { get; } = SKPath.ParseSvgPathData(
        "M10.1836 13.436H20.098C20.098 9.91613 18.5621 7.51636 14.3743 7.51636C9.42537 7.51634 4.3926 10.7381 " +
        "2 14.7553C2.74111 7.78266 7.74053 2.15332 15.2673 2.15332C21.7186 2.15332 27.8309 7.08585 27.8309 " +
        "15.2853V18.4026H10.2364C10.2364 22.6456 13.9377 24.3267 17.8142 24.3267C22.5317 24.3267 25.4532 22.2068 " +
        "25.4532 22.2068V28.15C25.4532 28.15 22.1552 30.1533 16.8985 30.1533C10.0672 30.1533 5.24517 25.2576 " +
        "5.24517 19.1697C5.24517 14.3911 8.13564 10.5759 12.1393 9.00165C10.1904 11.142 10.1835 13.436 10.1835 " +
        "13.436H10.1836Z");
}
