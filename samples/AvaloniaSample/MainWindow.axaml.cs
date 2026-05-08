using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace AvaloniaSample;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Dev-loop hook: when LVC_SCREENSHOT is set, render the window to PNG
        // shortly after open and exit. Lets agents/scripts capture the rendered
        // sample without external screenshot tooling. Pair with LVC_SAMPLE to
        // land on a specific repro view.
        var screenshotPath = Environment.GetEnvironmentVariable("LVC_SCREENSHOT");
        if (!string.IsNullOrWhiteSpace(screenshotPath))
            Opened += async (_, _) => await CaptureAndExitAsync(screenshotPath);
    }

    private async Task CaptureAndExitAsync(string path)
    {
        // Allow the chart's measure + initial animation frames to settle before
        // capture. 2s is conservative — sample animations are typically ~700 ms.
        await Task.Delay(2000);

        try
        {
            var size = new PixelSize(
                Math.Max(1, (int)Bounds.Width),
                Math.Max(1, (int)Bounds.Height));
            var rtb = new RenderTargetBitmap(size);
            rtb.Render(this);

            var fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            rtb.Save(fullPath);
            Console.WriteLine($"[LVC_SCREENSHOT] saved {size.Width}x{size.Height} to {fullPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LVC_SCREENSHOT] capture failed: {ex.Message}");
            Environment.ExitCode = 1;
        }

        await Dispatcher.UIThread.InvokeAsync(Close);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
