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
        // Wait for the chart's measure + initial animation frames to settle.
        // The default series animation is 800 ms; some samples kick off async
        // data loads (~1 s) before animating. 3 s leaves a margin for both,
        // overridable via LVC_SCREENSHOT_DELAY_MS for slower machines or
        // longer-loading samples.
        await Task.Delay(GetDelayMs());

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

    private static int GetDelayMs()
    {
        var raw = Environment.GetEnvironmentVariable("LVC_SCREENSHOT_DELAY_MS");
        return int.TryParse(raw, out var ms) && ms > 0 ? ms : 3000;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
