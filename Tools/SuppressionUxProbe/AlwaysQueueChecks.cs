using System.Collections;
using System.IO;
using Expression = System.Linq.Expressions.Expression;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

internal sealed class AlwaysQueueChecks
{
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<string> _started = [];
    public Task Execute(object options, object report, CancellationToken token)
    {
        var name = Path.GetFileName((string)options.GetType().GetProperty("OutputPath")!.GetValue(options)!);
        lock (_started) _started.Add(name);
        if (name == "first.mp4") return _release.Task.WaitAsync(token);
        return name == "fail.mp4" ? Task.FromException(new IOException("expected fake failure")) : Task.CompletedTask;
    }
    private static object New(Type type, params object[] values)
    {
        var ctor = type.GetConstructors().OrderByDescending(x => x.GetParameters().Length).First();
        return ctor.Invoke(ctor.GetParameters().Select((p, i) => i < values.Length ? values[i] : p.DefaultValue).ToArray());
    }
    private static void Wait(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!predicate())
        {
            if (DateTime.UtcNow > deadline) throw new Exception("Queue check timeout");
            var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false); Dispatcher.PushFrame(frame); Thread.Sleep(5);
        }
    }
    internal static void Run(Assembly gui, string output)
    {
        foreach (var cancelFirst in new[] { false, true })
        {
            var check = new AlwaysQueueChecks();
            var pageType = gui.GetType("SekaiToolsGUI.View.Suppress.SuppressPage", true)!;
            var modelType = gui.GetType("SekaiToolsGUI.ViewModel.Suppress.SuppressPageModel", true)!;
            var ctor = pageType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single(x => x.GetParameters().Length == 2);
            var queueType = ctor.GetParameters()[1].ParameterType;
            var delegateType = queueType.GetConstructors()[0].GetParameters()[0].ParameterType;
            var parameters = delegateType.GetMethod("Invoke")!.GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
            var body = Expression.Call(Expression.Constant(check), typeof(AlwaysQueueChecks).GetMethod(nameof(Execute))!, Expression.Convert(parameters[0], typeof(object)), Expression.Convert(parameters[1], typeof(object)), parameters[2]);
            var queue = New(queueType, Expression.Lambda(delegateType, body, parameters).Compile());
            var model = New(modelType); var page = ctor.Invoke([model, queue]);
            var enqueue = pageType.GetMethod("EnqueueSuppression", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var optionsType = enqueue.GetParameters()[0].ParameterType;
            var encodingType = optionsType.GetConstructors().OrderByDescending(x => x.GetParameters().Length).First().GetParameters()[3].ParameterType;
            var directory = Path.Combine(Path.GetFullPath(output), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            var source = Path.Combine(directory, "source.mp4"); File.WriteAllText(source, "validation fixture");
            void Add(string name) => enqueue.Invoke(page, [New(optionsType, source, "", Path.Combine(directory, name), New(encodingType)), null]);
            var pending = (IList)modelType.GetProperty("PendingJobs")!.GetValue(model)!;
            var finished = (IList)modelType.GetProperty("CompletedJobs")!.GetValue(model)!;
            try
            {
                Add("first.mp4"); Wait(() => { lock (check._started) return check._started.Count == 1; });
                Add("second.mp4"); Add("fail.mp4"); Add("last.mp4");
                if (pending.Count != 3) throw new Exception("Jobs did not wait behind active task");
                var selection = modelType.GetProperty("SelectedJob")!.GetValue(model);
                var editing = pending[0]!; modelType.GetMethod("BeginEdit")!.Invoke(model, [editing]);
                if (cancelFirst) modelType.GetProperty("RunningJob")!.GetValue(model)!.GetType().GetMethod("Cancel")!.Invoke(modelType.GetProperty("RunningJob")!.GetValue(model), null);
                else check._release.SetResult();
                Wait(() => finished.Count == 1);
                lock (check._started) if (check._started.Count != 1) throw new Exception("Editing head was bypassed");
                modelType.GetMethod("EndEdit")!.Invoke(model, [editing]);
                pageType.GetMethod("StartNextQueuedJob", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null);
                Wait(() => finished.Count == 4);
                lock (check._started) if (!check._started.SequenceEqual(new[] { "first.mp4", "second.mp4", "fail.mp4", "last.mp4" })) throw new Exception("Queue order or continuation failed");
                if (!ReferenceEquals(selection, modelType.GetProperty("SelectedJob")!.GetValue(model))) throw new Exception("Queue stole selected task");
                if (pending.Count != 0 || modelType.GetProperty("RunningJob")!.GetValue(model) != null) throw new Exception("Queue did not drain");
                Console.WriteLine($"Always queue passed: initial automatic start, sequential order, editing guard, {(cancelFirst ? "cancel" : "success")} continuation and failure continuation.");
            }
            finally { check._release.TrySetResult(); queueType.GetMethod("Dispose")!.Invoke(queue, null); }
        }
    }
}
