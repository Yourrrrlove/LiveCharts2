using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LiveChartsCore.SkiaSharpView.Avalonia;

namespace AvaloniaSample.VisualTest.Issue2131Repro;

public partial class View : UserControl
{
    public View()
    {
        InitializeComponent();
    }

    public PieChart ConstrainedChart => this.Find<PieChart>("constrainedChart")!;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
