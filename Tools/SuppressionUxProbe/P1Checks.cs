using System.Collections;
using System.IO;
using System.Reflection;

internal static class P1Checks
{
    private static object New(Type type, params object[] values)
    {
        var ctor = type.GetConstructors().OrderByDescending(x => x.GetParameters().Length).First();
        return ctor.Invoke(ctor.GetParameters().Select((p, i) => i < values.Length ? values[i] : p.DefaultValue).ToArray());
    }
    internal static void Run(Assembly gui, string output)
    {
        var modelType = gui.GetType("SekaiToolsGUI.ViewModel.Suppress.SuppressPageModel", true)!;
        var jobModel = gui.GetType("SekaiToolsGUI.ViewModel.Suppress.SuppressionJobModel", true)!;
        var job = jobModel.GetConstructors()[0].GetParameters()[0].ParameterType;
        var options = job.GetConstructors()[0].GetParameters()[0].ParameterType;
        var encoding = options.GetConstructors().OrderByDescending(x => x.GetParameters().Length).First().GetParameters()[3].ParameterType;
        object Model() => New(modelType);
        var directory = Path.Combine(Path.GetFullPath(output), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "source.mp4"); File.WriteAllText(source, "validation-only fixture");
        object Item(string name, string? input = null, bool overwrite = false) => New(jobModel, New(job, New(options, input ?? source, "", Path.Combine(directory, name), New(encoding), 100, overwrite, 1080)));
        object? Call(object model, string method, params object[] values) => modelType.GetMethod(method)!.Invoke(model, values);
        IList Pending(object model) => (IList)modelType.GetProperty("PendingJobs")!.GetValue(model)!;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        void Reject(Action action) { try { action(); } catch (TargetInvocationException) { return; } throw new Exception("Expected rejected submission"); }
        var model = Model(); var first = Item("first.mp4"); var second = Item("second.mp4");
        Pending(model).Add(first); Pending(model).Add(second);
        Check((bool)Call(model, "BeginEdit", first)!, "BeginEdit failed");
        Check(!(bool)Call(model, "TryStart", first)!, "Editing task was started");
        Reject(() => Call(model, "CommitPending", Item("replacement.mp4", Path.Combine(directory, "missing.mp4")), first));
        Check(ReferenceEquals(Pending(model)[0], first) && Pending(model).Count == 2, "Invalid input lost original");
        Reject(() => Call(model, "CommitPending", Item("second.mp4"), first));
        Check(ReferenceEquals(Pending(model)[0], first), "Conflict lost original");
        var lateOutput = Path.Combine(directory, "late.mp4"); var replacement = Item("late.mp4");
        File.WriteAllText(lateOutput, "existing output");
        Reject(() => Call(model, "CommitPending", replacement, first));
        Check(File.ReadAllText(lateOutput) == "existing output" && ReferenceEquals(Pending(model)[0], first), "Unauthorized overwrite or task loss");
        replacement = Item("late.mp4", overwrite: true);
        Call(model, "CommitPending", replacement, first);
        Check(ReferenceEquals(Pending(model)[0], replacement) && ReferenceEquals(Pending(model)[1], second), "Edit changed queue order");
        Call(model, "EndEdit", first);
        Check((bool)Call(model, "TryStart", replacement)!, "Replacement cannot start");
        Check(!(bool)Call(model, "BeginEdit", replacement)!, "Running task can be edited");
        var cancelled = Model(); var waiting = Item("cancel-edit.mp4"); Pending(cancelled).Add(waiting);
        Call(cancelled, "BeginEdit", waiting); Call(cancelled, "EndEdit", waiting);
        Check(ReferenceEquals(Pending(cancelled)[0], waiting) && (bool)Call(cancelled, "TryStart", waiting)!, "Cancel edit did not restore scheduling");
        var advancing = Model(); var active = Item("active.mp4"); var editing = Item("editing.mp4");
        Pending(advancing).Add(active); Pending(advancing).Add(editing);
        Call(advancing, "TryStart", active); Call(advancing, "BeginEdit", editing);
        var progressType = jobModel.GetProperty("Progress")!.PropertyType;
        var stateType = progressType.GetConstructors().OrderByDescending(x => x.GetParameters().Length).First().GetParameters()[3].ParameterType;
        jobModel.GetProperty("Progress")!.SetValue(active, New(progressType, 100, 100, 30d, Enum.Parse(stateType, "Completed"), "done"));
        Check(!(bool)Call(advancing, "TryStart", editing)!, "Previous completion started editing task");
        Call(advancing, "EndEdit", editing);
        Check((bool)Call(advancing, "TryStart", editing)!, "Editing task did not resume after unlock");
        Console.WriteLine("P1 checks passed: edit lock, missing input, output conflict, late file authorization, in-place ordering, running state, cancel release.");
    }
}
