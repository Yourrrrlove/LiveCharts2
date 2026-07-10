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
using LiveChartsCore.Measure;

namespace LiveChartsCore.Kernel.Sketches;

/// <summary>
/// Produces the <see cref="Scaler"/> an axis uses to map between data values and pixels. Assign one
/// to <see cref="ICartesianAxis.ScalerProvider"/> to plug in a custom (for example non-linear)
/// coordinate mapping without a UI-framework-specific axis type; every scaler the engine builds for
/// the axis — series, gridlines, hit-testing and zoom alike — flows through it.
/// </summary>
public interface IScalerProvider
{
    /// <summary>
    /// Builds a scaler for <paramref name="axis"/>.
    /// </summary>
    /// <param name="axis">The axis to scale.</param>
    /// <param name="chart">The chart the axis belongs to. An axis can be shared by several charts, so the
    /// scaler is built per chart and a provider that keeps per-chart state must key it on this.</param>
    /// <param name="drawMarginLocation">The draw margin location.</param>
    /// <param name="drawMarginSize">The draw margin size.</param>
    /// <param name="bounds">Optional bounds to scale against; when null the axis' own limits are used.</param>
    Scaler GetScaler(ICartesianAxis axis, Chart chart, LvcPoint drawMarginLocation, LvcSize drawMarginSize, Bounds? bounds);
}
