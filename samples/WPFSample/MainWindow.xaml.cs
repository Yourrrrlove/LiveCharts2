using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WPFSample;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Samples = ViewModelsSamples.Index.Samples;
        DataContext = this;

        // Dev-loop hook: LVC_SAMPLE selects an initial sample by path
        // (e.g. LVC_SAMPLE=Bars/Basic). Lets agents/scripts launch the app
        // pointed at a specific repro without UI navigation.
        var initial = Environment.GetEnvironmentVariable("LVC_SAMPLE");
        if (!string.IsNullOrWhiteSpace(initial) && Samples.Contains(initial))
            content.Content = Activator
                .CreateInstance(null, $"WPFSample.{initial.Replace('/', '.')}.View")?
                .Unwrap();

        // Dev-loop hook: when LVC_SCREENSHOT is set, render the window to PNG
        // shortly after load and exit. Lets agents/scripts capture the rendered
        // sample without external screenshot tooling.
        var screenshotPath = Environment.GetEnvironmentVariable("LVC_SCREENSHOT");
        if (!string.IsNullOrWhiteSpace(screenshotPath))
            Loaded += async (_, _) => await CaptureAndExitAsync(screenshotPath);
    }

    public string[] Samples { get; set; }

    private void Border_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement).DataContext is not string ctx) throw new Exception("Sample not found");
        content.Content = Activator.CreateInstance(null, $"WPFSample.{ctx.Replace('/', '.')}.View").Unwrap();
    }

    private async Task CaptureAndExitAsync(string path)
    {
        // Wait for the chart's measure + initial animation frames to settle.
        // See AvaloniaSample/MainWindow.axaml.cs for rationale (default 3s,
        // override via LVC_SCREENSHOT_DELAY_MS for slower hosts).
        await Task.Delay(GetDelayMs());

        try
        {
            var w = (int)Math.Max(1, ActualWidth);
            var h = (int)Math.Max(1, ActualHeight);
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(this);

            var fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            using var fs = File.Create(fullPath);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            encoder.Save(fs);
            Console.WriteLine($"[LVC_SCREENSHOT] saved {w}x{h} to {fullPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LVC_SCREENSHOT] capture failed: {ex.Message}");
            Environment.ExitCode = 1;
        }

        Close();
    }

    private static int GetDelayMs()
    {
        var raw = Environment.GetEnvironmentVariable("LVC_SCREENSHOT_DELAY_MS");
        return int.TryParse(raw, out var ms) && ms > 0 ? ms : 3000;
    }
}
