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
using LiveChartsCore.Drawing;
using LiveChartsCore.Geo;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.Motion;
using LiveChartsCore.SkiaSharpView.Maui;
using Microsoft.Maui.ApplicationModel;

namespace LiveChartsGeneratedCode;

// ===============================================
// this file contains the MAUI specific code
// ===============================================

/// <inheritdoc cref="IChartView"/>
public abstract partial class SourceGenMapChart : ChartView, IGeoMapView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SourceGenMapChart"/> class.
    /// </summary>
    protected SourceGenMapChart()
    {
        Content = new MotionCanvas();

        SizeChanged += (s, e) =>
            CoreChart.Update();

        InitializeChartControl();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private MotionCanvas CanvasView => (MotionCanvas)Content;

    /// <inheritdoc cref="IDrawnView.CoreCanvas"/>
    public CoreMotionCanvas CoreCanvas => CanvasView.CanvasCore;

    bool IGeoMapView.DesignerMode => false;
    bool IGeoMapView.IsDarkMode => false;
    LvcSize IDrawnView.ControlSize => new() { Width = (float)Width, Height = (float)Height };

    private void OnLoaded(object? sender, EventArgs e) =>
        CoreChart?.Load();

    private void OnUnloaded(object? sender, EventArgs e)
    {
        CoreChart?.Unload();
#if IOS || MACCATALYST
        // See SourceGenChart.OnUnloaded for the rationale (#1725).
        Handler?.DisconnectHandler();
#endif
    }

    void IGeoMapView.InvokeOnUIThread(Action action) =>
        MainThread.BeginInvokeOnMainThread(action);

    internal override void OnPressed(object? sender, LiveChartsCore.Native.Events.PressedEventArgs args) =>
        CoreChart?.InvokePointerDown(args.Location);

    internal override void OnMoved(object? sender, LiveChartsCore.Native.Events.ScreenEventArgs args) =>
        CoreChart?.InvokePointerMove(args.Location);

    internal override void OnReleased(object? sender, LiveChartsCore.Native.Events.PressedEventArgs args) =>
        CoreChart?.InvokePointerUp(args.Location);

    internal override void OnExited(object? sender, LiveChartsCore.Native.Events.EventArgs args) =>
        CoreChart?.InvokePointerLeft();

    internal override void OnScrolled(object? sender, LiveChartsCore.Native.Events.ScrollEventArgs args) =>
        CoreChart?.InvokePointerWheel(
            args.Location,
            args.ScrollDelta > 0 ? ZoomDirection.ZoomIn : ZoomDirection.ZoomOut);
}
