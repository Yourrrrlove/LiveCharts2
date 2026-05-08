namespace MauiSample;

public partial class AppShell : Shell
{
    private bool _isLoaded = false;
    private readonly Dictionary<string, string> _routesSamples = [];

    public AppShell()
    {
        InitializeComponent();
        PropertyChanged += AppShell_PropertyChanged;
    }

    private void AppShell_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isLoaded) return;
        _isLoaded = true;

        var samples = ViewModelsSamples.Index.Samples;

        // Dev-loop hook: LVC_SAMPLE selects an initial sample by path
        // (e.g. LVC_SAMPLE=Bars/Basic). Lets agents/scripts launch the app
        // pointed at a specific repro. Falls back to the first sample if
        // the env var is unset or the path isn't in the index.
        var initial = Environment.GetEnvironmentVariable("LVC_SAMPLE");
        var initialIndex = string.IsNullOrWhiteSpace(initial)
            ? 0
            : Math.Max(0, Array.IndexOf(samples, initial));

        for (var i1 = 0; i1 < samples.Length; i1++)
        {
            var item = samples[i1];
            var t = Type.GetType($"MauiSample.{item.Replace('/', '.')}.View")!;
            //var i = Activator.CreateInstance(t);
            Routing.RegisterRoute(item, t);

            var shell_section = new ShellSection { Title = item };

            ShellContent content;

            try
            {
                content = new ShellContent()
                {
                    Content = i1 == initialIndex ? Activator.CreateInstance(t) : null
                };
            }
            catch (Exception)
            {
                throw;
            }

            shell_section.Items.Add(content);

            Items.Add(shell_section);
            _routesSamples.Add("//" + content.Route, item);

            //if (i > 4) break;
        }

        if (initialIndex > 0) CurrentItem = Items[initialIndex];

        Navigating += AppShell_Navigating;
    }

    private void AppShell_Navigating(object? sender, ShellNavigatingEventArgs e)
    {
        var shell = (AppShell?)sender ?? throw new Exception("Sell not found");

        var r = shell.Items.Select(x => x.CurrentItem.CurrentItem.Route).ToArray();
        var next = Items.FirstOrDefault(x => "//" + x.CurrentItem.CurrentItem.Route == e.Target.Location.OriginalString);

        var item = _routesSamples[e.Target.Location.OriginalString];
        var t = Type.GetType($"MauiSample.{item.Replace('/', '.')}.View");
        var i = Activator.CreateInstance(t!);
        var c = next!.Items[0].Items[0];

        c.Content = i;
    }
}
