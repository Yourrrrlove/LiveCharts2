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
        // (e.g. LVC_SAMPLE=Bars/Basic). Lets agents/scripts launch the app
        // pointed at a specific repro without UI navigation.
        var initial = Environment.GetEnvironmentVariable("LVC_SAMPLE");
        if (!string.IsNullOrWhiteSpace(initial) && Array.IndexOf(Samples, initial) >= 0)
            SelectedSample = initial;
    }

    public string[] Samples { get; } = ViewModelsSamples.Index.Samples;
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
