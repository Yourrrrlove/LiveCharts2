// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using LiveChartsCore;
using LiveChartsCore.Console;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel.Sketches;

namespace LiveChartsGeneratedCode;

// Bridge: this is the partial half supplied by the console engine. The shared/sgp halves
// (linked from src/skiasharp/_Shared/) supply the IChartView-facing properties.
public abstract partial class SourceGenChart : InMemoryConsoleChart, IDrawnView
{
    protected SourceGenChart()
    {
        // Defaults (AutoUpdateEnabled = true, EasingFunction from theme) are kept so live
        // RenderLoopAsync drives animations naturally. The one-shot Render() opts out by
        // setting Canvas.DisableAnimations = true at the call site.

        InitializeChartControl();
        InitializeObservedProperties();

        StartObserving();
        CoreChart?.Load();
    }

    bool IChartView.DesignerMode => false;
    bool IChartView.IsDarkMode => true; // terminals are usually dark.
    LvcColor IChartView.BackColor => Background;
    LvcSize IDrawnView.ControlSize => new() { Width = Width, Height = Height };

    void IChartView.InvokeOnUIThread(Action action) => action();

    protected override Chart GetCoreChart() => CoreChart;
}
