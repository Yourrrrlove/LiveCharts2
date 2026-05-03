namespace MauiSample.Test.Dispose;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class View : ContentPage
{
    private ContentView _currentView;

    public View()
    {
        InitializeComponent();
        _currentView = new NewPage1();
        Grid.SetRow(_currentView, 1);
        container.Add(_currentView);
    }

    private void Button_Clicked(object sender, EventArgs e) => _ = ChangeContent();

    public ContentView ChangeContent()
    {
        var swappedOut = _currentView;
        _ = container.Remove(_currentView);
        _currentView = new NewPage1();
        Grid.SetRow(_currentView, 1);
        container.Add(_currentView);
        return swappedOut;
    }
}
