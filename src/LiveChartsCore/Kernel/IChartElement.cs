﻿// The MIT License(MIT)
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

using System.ComponentModel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Painting;

namespace LiveChartsCore.Kernel;

/// <summary>
/// Defines a visual element in a chart.
/// </summary>
public interface IChartElement : INotifyPropertyChanged
{
    /// <summary>
    /// Gets or sets the object that contains data about the control.
    /// </summary>
    object? Tag { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the element is visible.
    /// </summary>
    bool IsVisible { get; set; }

    /// <summary>
    /// Invalidates the <see cref="IChartElement"/> in the user interface.
    /// </summary>
    /// <param name="chart">The chart.</param>
    void Invalidate(Chart chart);

    /// <summary>
    /// Deletes the <see cref="Paint"/> instances that changed from the user interface.
    /// </summary>
    /// <param name="chart">The chart.</param>
    void RemoveOldPaints(IChartView chart);

    /// <summary>
    /// Removes the element from the UI.
    /// </summary>
    /// <param name="chart">The chart.</param>
    void RemoveFromUI(Chart chart);
}
