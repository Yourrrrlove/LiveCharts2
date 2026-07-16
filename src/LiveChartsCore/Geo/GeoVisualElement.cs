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

using System;
using LiveChartsCore.Kernel;
using LiveChartsCore.Painting;
using LiveChartsCore.VisualElements;

namespace LiveChartsCore.Geo;

/// <summary>
/// Wraps a <see cref="VisualElements.Visual"/> (or the older
/// <see cref="VisualElements.VisualElement"/>) and positions it at a geographic
/// (longitude, latitude) on a <see cref="GeoMapChart"/>. Each measure pass
/// re-projects the coordinate via the chart's current projector and writes
/// the result onto the inner visual, so the overlay follows zoom, pan, and
/// orthographic rotation. When the coordinate isn't visible (e.g. the back
/// hemisphere on <see cref="MapProjection.Orthographic"/>), the inner
/// visual is removed from the UI for the frame so it doesn't ghost at
/// its last on-screen position.
/// </summary>
public class GeoVisualElement : ChartElement
{
    /// <summary>
    /// Initializes a new <see cref="GeoVisualElement"/> wrapping the given
    /// inner visual.
    /// </summary>
    /// <param name="visual">The visual to position at (Longitude, Latitude).</param>
    /// <remarks>
    /// A <see cref="VisualElements.Visual"/> is positioned through its drawn element, so it must
    /// not set its own location in <c>Measure</c> or it will overwrite the projected coordinate.
    /// </remarks>
    public GeoVisualElement(Visual visual)
        : this((ChartElement?)visual)
    { }

    /// <summary>
    /// Initializes a new <see cref="GeoVisualElement"/> wrapping the given
    /// inner visual element.
    /// </summary>
    /// <param name="visual">The visual element to position at (Longitude, Latitude).</param>
    [Obsolete($"Replaced by the {nameof(Visual)} overload.")]
    public GeoVisualElement(VisualElement visual)
        : this((ChartElement?)visual)
    { }

    private GeoVisualElement(ChartElement? visual)
    {
        Visual = visual ?? throw new ArgumentNullException(nameof(visual));
    }

    /// <summary>
    /// Gets the wrapped visual, a <see cref="VisualElements.Visual"/> or a
    /// <see cref="VisualElements.VisualElement"/>, the constructors take no other type.
    /// </summary>
    public ChartElement Visual { get; }

    /// <summary>Gets or sets the longitude (in degrees) where the visual is anchored.</summary>
    public double Longitude { get; set => SetProperty(ref field, value); }

    /// <summary>Gets or sets the latitude (in degrees) where the visual is anchored.</summary>
    public double Latitude { get; set => SetProperty(ref field, value); }

    /// <inheritdoc cref="ChartElement.Invalidate(Chart)"/>
    public override void Invalidate(Chart chart)
    {
        if (chart is not GeoMapChart geoMap)
            throw new InvalidOperationException(
                $"{nameof(GeoVisualElement)} can only be used on a {nameof(GeoMapChart)}.");

        var pixel = geoMap.Project(Longitude, Latitude);
        if (pixel is null)
        {
            // Off the visible region (e.g. orthographic back hemisphere).
            // Pull the inner visual off the canvas for this frame so it
            // doesn't render at its last on-screen position.
            Visual.RemoveFromUI(chart);
            return;
        }

        SetLocation(pixel.Value.X, pixel.Value.Y);
        Visual.Invalidate(chart);

        // Chart.AddVisual normally calls RemoveOldPaints after Invalidate to
        // drop paint tasks the visual no longer references (e.g. user swapped
        // Fill from green to red — green paint stays on the canvas otherwise).
        // The wrapped visual goes through our Invalidate, not AddVisual, so
        // mirror that step explicitly.
        Visual.RemoveOldPaints(chart.View);
    }

    private void SetLocation(float x, float y)
    {
        // CS0618: VisualElement is obsolete and still supported, that is what this branch is for.
#pragma warning disable CS0618
        switch (Visual)
        {
            case VisualElement visualElement:
                visualElement.X = x;
                visualElement.Y = y;
                break;

            // A Visual has no location of its own, it lives on the drawn element, the same place
            // Chart.AddTitleToChart writes to position the title.
            case Visual visual when visual.DrawnElement is not null:
                visual.DrawnElement.X = x;
                visual.DrawnElement.Y = y;
                break;
        }
#pragma warning restore CS0618
    }

    /// <inheritdoc cref="ChartElement.RemoveFromUI(Chart)"/>
    // base.RemoveFromUI iterates GetPaintTasks() (which delegates to Visual),
    // so calling Visual.RemoveFromUI is enough; the base call would double-
    // remove the same paint tasks.
    public override void RemoveFromUI(Chart chart) => Visual.RemoveFromUI(chart);

    /// <inheritdoc cref="ChartElement.GetPaintTasks"/>
    protected internal override Paint?[] GetPaintTasks() => Visual.GetPaintTasks();
}
