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

namespace LiveChartsCore.SkiaSharpView.WPF;

/// <summary>
/// A helper class to get the screen resolution.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ResolutionHelper"/> class.
/// </remarks>
/// <param name="dpix">The horizontal resolution.</param>
/// <param name="dpiy">The vertical resolution.</param>
public class ResolutionHelper(float dpix, float dpiy)
{
    /// <summary>
    /// Gets or sets the horizontal dpi.
    /// </summary>
    public float dpix { get; set; } = dpix;

    /// <summary>
    /// Gets or sets the vertical dpi.
    /// </summary>
    public float dpiy { get; set; } = dpiy;
}
