using System.Windows.Threading;

namespace SekaiToolsGUI.Service;

internal static class TaskMemoryCleanup
{
    internal static async Task AfterResetAsync(Dispatcher dispatcher)
    {
        // Let WPF apply cleared images/items before collecting their old object graphs.
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        await Task.Run(() =>
        {
            // Only at an explicit task reset, never during recognition or on each frame.
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        });
    }
}
