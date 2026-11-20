using System.Drawing;
using FlaUI.Core.Input;

namespace SekaiTools.UITests.Helpers;

internal static class FileDropDriver
{
    public static bool DragFile(string path, Point target)
    {
        var ready = new TaskCompletionSource<Point>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dragging = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var source = new System.Windows.Controls.Border { Background = System.Windows.Media.Brushes.LightGray };
                var window = new System.Windows.Window
                {
                    Title = "FlaUI 文件拖放源",
                    Width = 160,
                    Height = 90,
                    Topmost = true,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                    Content = source
                };
                source.MouseLeftButtonDown += (_, _) =>
                {
                    var data = new System.Windows.DataObject(System.Windows.DataFormats.FileDrop, new[] { path });
                    dragging.TrySetResult();
                    var effect = System.Windows.DragDrop.DoDragDrop(source, data, System.Windows.DragDropEffects.Copy);
                    finished.TrySetResult(effect == System.Windows.DragDropEffects.Copy);
                    window.Close();
                };
                window.ContentRendered += (_, _) =>
                {
                    var origin = source.PointToScreen(new System.Windows.Point(source.ActualWidth / 2, source.ActualHeight / 2));
                    ready.TrySetResult(new Point((int)origin.X, (int)origin.Y));
                };
                window.Closed += (_, _) => window.Dispatcher.InvokeShutdown();
                window.Show();
                System.Windows.Threading.Dispatcher.Run();
            }
            catch (Exception exception)
            {
                ready.TrySetException(exception);
                finished.TrySetException(exception);
            }
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try
        {
            Mouse.Position = ready.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Mouse.Down(MouseButton.Left);
            try
            {
                dragging.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                Mouse.MoveTo(target);
            }
            finally
            {
                Mouse.Up(MouseButton.Left);
            }
            return finished.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        finally
        {
            System.Windows.Threading.Dispatcher.FromThread(thread)?.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Normal);
            thread.Join(TimeSpan.FromSeconds(5));
        }
    }
}
