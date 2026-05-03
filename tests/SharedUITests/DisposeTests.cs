using Factos;
using SharedUITests.Helpers;
using Xunit;

#if MAUI_UI_TESTING
using Microsoft.Maui.Controls;
#endif

// to run these tests, see the UITests project, specially the program.cs file.
// to enable IDE intellisense for these tests, go to Directory.Build.props and set UITesting to true.

namespace SharedUITests;

public class DisposeTests
{
    public AppController App => AppController.Current;

#if MAUI_UI_TESTING
    // https://github.com/Live-Charts/LiveCharts2/issues/1725
    //
    // A chart removed from the visual tree must be collectable — its inner MotionCanvas /
    // SKCanvasView occupy substantial memory and the original report describes them piling up
    // page-after-page. Today the symptom does not reproduce on Maui 10 because Maui's
    // Application.RequestedThemeChanged uses a WeakEventManager, so a chart's `this`-capturing
    // theme subscription does not actually root the chart. This test is a canary: if a future
    // Maui (or our own code) regresses to holding charts strongly via an app-level event,
    // accumulator field, or static collection, this test fires.
    //
    // We weak-ref the charts directly, not the parent page. In Maui 10+, Element.Parent is a
    // WeakReference, so a strong root on a chart does not propagate up to its parent — testing
    // page survival would silently miss a real chart leak.
    [AppTestMethod]
    public async Task UnloadedChartsShouldBeCollectable_Issue1725()
    {
        var sut = await App.NavigateTo<Samples.Test.Dispose.View>();
        await Task.Delay(1000);

        var weakRefs = new List<WeakReference>();

        const int Iterations = 5;
        for (var i = 0; i < Iterations; i++)
        {
            // Inline the call so the swapped-out page is never bound to a named local; a local
            // declared inside this loop would be hoisted by the async state machine into a
            // long-lived field that survives across the awaits below, masking the leak.
            CaptureChartWeakRefs(sut.ChangeContent(), weakRefs);
            await Task.Delay(500);
        }

        // One untracked extra swap to push the most-recent tracked instance out of any
        // transient reference Maui's layout/dispatcher holds for the latest unload.
        _ = sut.ChangeContent();

        // Let Maui's dispatcher drain any queued unload work.
        await Task.Delay(2000);

        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        var alive = weakRefs.Count(r => r.IsAlive);
        Assert.True(
            alive == 0 && weakRefs.Count >= Iterations,
            $"{alive}/{weakRefs.Count} unloaded chart instances were still alive after GC. " +
            "An unloaded chart must not be rooted by anything in the app. The likely cause is " +
            "a `this`-capturing event subscription in the chart that is never detached, or a " +
            "static collection / accumulator field that the chart was added to. See " +
            "https://github.com/Live-Charts/LiveCharts2/issues/1725.");
    }

    private static void CaptureChartWeakRefs(ContentView swappedOutPage, List<WeakReference> weakRefs)
    {
        if (swappedOutPage.Content is not Grid grid) return;
        foreach (var child in grid.Children)
            if (child is View view)
                weakRefs.Add(new WeakReference(view));
    }
#endif
}
