using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

internal static class OverwriteChecks
{
    private static void PumpUntil(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!task.IsCompleted)
        {
            if (DateTime.UtcNow > deadline) throw new Exception("Submission timeout");
            var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false); Dispatcher.PushFrame(frame);
        }
    }
    internal static void Run(Assembly gui, Assembly wpf, Application app, string output)
    {
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Activator.CreateInstance(wpf.GetType("Wpf.Ui.Markup.ThemesDictionary", true)!)!);
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Activator.CreateInstance(wpf.GetType("Wpf.Ui.Markup.ControlsDictionary", true)!)!);
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/SekaiToolsGUI;component/Converter/Converters.xaml", UriKind.Relative) });
        var host = Activator.CreateInstance(wpf.GetType("Wpf.Ui.Controls.ContentDialogHost", true)!)!;
        var type = gui.GetType("SekaiToolsGUI.View.Suppress.Components.SuppressionTaskDialog", true)!;
        var dialog = (FrameworkElement)Activator.CreateInstance(type, host)!;
        var model = type.GetProperty("ViewModel")!.GetValue(dialog)!;
        var set = model.GetType().BaseType!.GetMethod("SetProperty", BindingFlags.Instance | BindingFlags.NonPublic)!.MakeGenericMethod(typeof(string));
        var directory = Path.Combine(Path.GetFullPath(output), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "source.mp4"); File.WriteAllText(source, "validation fixture");
        var target = Path.Combine(directory, "output.mp4"); File.WriteAllText(target, "original output");
        set.Invoke(model, [source, "SourceVideo"]); set.Invoke(model, [target, "OutputPath"]);
        var confirm = type.GetProperty("ConfirmOverwrite", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var submit = type.GetMethod("TrySubmitAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var attempts = 0; var commits = 0;
        type.GetProperty("CommitSubmission")!.SetValue(dialog, (Func<string>)(() => { commits++; return ""; }));
        bool Run() { var task = (Task<bool>)submit.Invoke(dialog, null)!; PumpUntil(task); return task.GetAwaiter().GetResult(); }
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        confirm.SetValue(dialog, (Func<string, Task<bool>>)(_ => { attempts++; return Task.FromResult(false); }));
        Check(!Run() && commits == 0 && attempts == 1 && dialog.IsEnabled, "Cancel did not preserve dialog");
        Check((string)model.GetType().GetProperty("OutputPath")!.GetValue(model)! == target && File.ReadAllText(target) == "original output", "Cancel changed form or file");
        confirm.SetValue(dialog, (Func<string, Task<bool>>)(_ => { attempts++; return Task.FromResult(true); }));
        Check(Run() && commits == 1 && (bool)type.GetProperty("OverwriteAuthorized")!.GetValue(dialog)!, "Confirmation did not authorize submission");
        var pending = new TaskCompletionSource<bool>();
        confirm.SetValue(dialog, (Func<string, Task<bool>>)(_ => pending.Task));
        var waiting = (Task<bool>)submit.Invoke(dialog, null)!;
        Check(!Run() && commits == 1, "Double submission was accepted");
        var changed = Path.Combine(directory, "changed.mp4"); set.Invoke(model, [changed, "OutputPath"]);
        pending.SetResult(true); PumpUntil(waiting);
        Check(!waiting.Result && commits == 1, "Authorization leaked to changed path");
        Check(Run() && commits == 2 && !(bool)type.GetProperty("OverwriteAuthorized")!.GetValue(dialog)!, "No-conflict submission acquired overwrite permission");
        Console.WriteLine("Overwrite dialog checks passed: cancel preserves form, explicit confirmation, double-click guard, changed-path rejection and no-conflict authorization.");
    }
}
