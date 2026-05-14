using System;
using System.ComponentModel;

namespace AvaloniaSample;

public class MainWindowViewModel : INotifyPropertyChanged
{
    public MainWindowViewModel()
    {
        Samples = [..
            ViewModelsSamples.Index.Samples,
            "VisualTest/Issue1986Repro"
        ];

        // Dev-loop hook: LVC_SAMPLE selects an initial sample by path
        // (e.g. LVC_SAMPLE=VisualTest/Issue1986Repro). The view is resolved by
        // type name (see SampleConverter), so this works for any sample —
        // including repro views that are intentionally not listed in the shared
        // index. The path is appended to Samples so the selector dropdown stays
        // in sync with the displayed view.
        var initial = Environment.GetEnvironmentVariable("LVC_SAMPLE");
        if (!string.IsNullOrWhiteSpace(initial)
            && Type.GetType($"AvaloniaSample.{initial.Replace('/', '.')}.View") is not null)
        {
            if (Array.IndexOf(Samples, initial) < 0)
                Samples = [.. Samples, initial];
            SelectedSample = initial;
        }
    }

    public string[] Samples { get; set; }

    public string? SelectedSample
    {
        get => field;
        set
        {
            if (field == value) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedSample)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
