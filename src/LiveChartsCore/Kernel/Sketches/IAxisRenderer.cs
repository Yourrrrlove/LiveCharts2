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

using LiveChartsCore.Drawing;

namespace LiveChartsCore.Kernel.Sketches;

/// <summary>
/// Owns the measure and draw of a cartesian axis. Assign one to <see cref="ICartesianAxis.Renderer"/>
/// to fully replace the built-in axis rendering — for example to draw multi-tier labels, dividers or
/// other custom gutter content — without needing a UI-framework-specific axis type.
/// </summary>
public interface IAxisRenderer
{
    /// <summary>
    /// Returns the size the axis should reserve for its content. Called in place of the built-in
    /// measure while a renderer is set.
    /// </summary>
    /// <param name="axis">The axis being measured.</param>
    /// <param name="chart">The chart.</param>
    LvcSize Measure(ICartesianAxis axis, Chart chart);

    /// <summary>
    /// Draws the axis. Called in place of the built-in draw while a renderer is set.
    /// </summary>
    /// <param name="axis">The axis being drawn.</param>
    /// <param name="chart">The chart.</param>
    void Draw(ICartesianAxis axis, Chart chart);

    /// <summary>
    /// Removes everything this renderer added to the chart (its geometries and paint tasks). The axis
    /// calls this when it stops using the renderer — because it was swapped for another renderer or for
    /// the built-in draw (e.g. on a theme change) — so no stale visuals are left on the canvas.
    /// </summary>
    /// <param name="axis">The axis the renderer was drawing.</param>
    /// <param name="chart">The chart.</param>
    void Clear(ICartesianAxis axis, Chart chart);
}
