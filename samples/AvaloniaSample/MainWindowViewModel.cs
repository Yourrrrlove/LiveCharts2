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
        // (e.g. LVC_SAMPLE=VisualTest/Issue1986Repro). Lets agents/scripts
        // launch the app pointed at a specific repro without UI navigation.
        var initial = Environment.GetEnvironmentVariable("LVC_SAMPLE");
        if (!string.IsNullOrWhiteSpace(initial) && Array.IndexOf(Samples, initial) >= 0)
            SelectedSample = initial;
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
