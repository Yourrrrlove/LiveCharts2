using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace WinUISample;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Samples = ViewModelsSamples.Index.Samples;
        grid.DataContext = this;

        // Dev-loop hook: LVC_SAMPLE selects an initial sample by path
        // (e.g. LVC_SAMPLE=Bars/Basic). Lets agents/scripts launch the app
        // pointed at a specific repro without UI navigation.
        var initial = Environment.GetEnvironmentVariable("LVC_SAMPLE");
        if (!string.IsNullOrWhiteSpace(initial) && Samples.Contains(initial))
            content.Content = Activator
                .CreateInstance(null, $"WinUISample.{initial.Replace('/', '.')}.View")?
                .Unwrap();
    }

    public string[] Samples { get; set; }

    private void Border_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if ((sender as FrameworkElement).DataContext is not string ctx) throw new Exception("Sample not found");

        content.Content = Activator.CreateInstance(null, $"WinUISample.{ctx.Replace('/', '.')}.View").Unwrap();
    }
}
