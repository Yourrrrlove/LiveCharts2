using System;
using System.ComponentModel;
using Uno.Extensions;
using Uno.Extensions.Reactive;
using ViewModelsSamples;

namespace UnoPlatformSample.Presentation;

public partial record MainModel : INotifyPropertyChanged
{
    private INavigator _navigator;
    private string selectedSample = "...";

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainModel(
        IStringLocalizer localizer,
        IOptions<AppConfig> appInfo,
        INavigator navigator)
    {
        _navigator = navigator;

        // Dev-loop hook: LVC_SAMPLE selects an initial sample by path
        // (e.g. LVC_SAMPLE=Bars/Basic). The view is resolved by type name (see
        // IndexToContentConverter), so this works for any sample — including
        // repro views that are intentionally not listed in the shared index.
        // The path is appended to Samples so the selector stays in sync with
        // the displayed view.
        var initial = Environment.GetEnvironmentVariable("LVC_SAMPLE");
        if (!string.IsNullOrWhiteSpace(initial)
            && Type.GetType($"WinUISample.{initial.Replace('/', '.')}.View") is not null)
        {
            if (Array.IndexOf(Samples, initial) < 0)
                Samples = [.. Samples, initial];
            SelectedSample = initial;
        }
    }

    public string[] Samples { get; private set; } = ViewModelsSamples.Index.Samples;
    public string SelectedSample 
    {
        get => selectedSample; 
        set { selectedSample = value; OnSelectedSampleChanged(value); } 
    }

    private void OnSelectedSampleChanged(string value)
    {
        _navigator.NavigateViewModelAsync<SecondModel>(this, data: new Entity(value));
    }
}
