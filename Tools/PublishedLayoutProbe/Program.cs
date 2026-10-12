using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media.Imaging;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length is < 2 or > 3) throw new ArgumentException("PublishedLayoutProbe <video> <new-report> [timeline]");
        var root = AppContext.BaseDirectory;
        var report = Path.GetFullPath(args[1]);
        var layoutReport = report + ".layout.json";
        if (File.Exists(layoutReport)) throw new IOException("Do not overwrite evidence");
        var guiPath = Path.Combine(root, "SekaiToolsGUI.dll");
        var gui = Assembly.LoadFrom(guiPath);
        // Execute the actual product ModuleInitializer; do not construct App.
        RuntimeHelpers.RunModuleConstructor(gui.ManifestModule.ModuleHandle);
        if (args.Length == 3)
        {
            if (args[2] is "factory" or "factory-reject")
            {
                FactoryAudit(gui, args[0], report, args[2] == "factory-reject");
                return;
            }
            if (args[2] != "timeline") throw new ArgumentException("Unknown probe mode");
            TimelineAudit(gui, args[0], report);
            return;
        }
        var probe = Assembly.LoadFrom(Path.Combine(root, "libs", "PeriodicColorWpfProbe.dll"));
        var main = probe.GetType("Program", true)!.GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic)!;
        var plugin = Path.Combine(root, "libs", "opencv_videoio_ffmpeg500_64.dll");
        if (!File.Exists(plugin)) plugin = Path.Combine(root, "runtimes", "win-x64", "native", "opencv_videoio_ffmpeg500_64.dll");
        main.Invoke(null, [new[] { args[0], plugin, report }]);
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name is "Emgu.CV" or "Emgu.CV.Wpf" or "SekaiToolsCore")
            .Select(a => new { Path = a.Location, Hash = Hash(a.Location) }).ToArray();
        if (assemblies.Length != 3 || assemblies.Any(a => !Path.GetFullPath(a.Path).StartsWith(Path.Combine(root, "libs") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Managed dependency did not load from published libs");
        var native = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
            .Single(m => m.ModuleName.Equals("cvextern.dll", StringComparison.OrdinalIgnoreCase)).FileName;
        using var experiment = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "native-experiment.json")));
        var qualifiedPaths = experiment.RootElement.GetProperty("ReplacedPaths").EnumerateArray().Select(p => p.GetString()!).ToArray();
        if (!qualifiedPaths.Contains(native, StringComparer.OrdinalIgnoreCase) ||
            Hash(native) != experiment.RootElement.GetProperty("NativeHash").GetString())
            throw new InvalidDataException("Loaded native runtime outside qualified replacement paths");
        File.WriteAllText(layoutReport, JsonSerializer.Serialize(new { PublishedRoot = root,
            ProductResolverAssembly = guiPath, ProductResolverHash = Hash(guiPath), Assemblies = assemblies,
            NativePath = native, NativeHash = Hash(native), DisplayReport = report, DisplayReportHash = Hash(report),
            PublishedDependencyLoadingPassed = true, LiveGuiQualified = false,
            ProductPeriodicCaptureIntegrated = false }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }

    private static void FactoryAudit(Assembly gui, string statePath, string report, bool reject)
    {
        if (File.Exists(report)) throw new IOException("Do not overwrite evidence");
        var core = Assembly.Load("SekaiToolsCore");
        var stateType = core.GetType("SekaiToolsCore.Process.ProcessingState", true)!;
        var stateJson = File.ReadAllText(statePath);
        var state = JsonSerializer.Deserialize(stateJson, stateType)!;
        using var json = JsonDocument.Parse(stateJson);
        var video = json.RootElement.GetProperty("VideoFilePath").GetString()!;
        var before = Hash(video);
        var identity = json.RootElement.GetProperty("Metadata").GetProperty("CaptureIdentity").GetString()!;
        var interval = int.Parse(identity.Split(':')[4]);
        var component = gui.GetType("SekaiToolsGUI.View.Subtitle.Components.SubtitleTask", true)!;
        var factory = component.GetMethod("GetCaptureFactory", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [state])!;
        object session;
        try { session = factory.GetType().GetMethod("Open")!.Invoke(factory, [video, interval])!; }
        catch (TargetInvocationException exception) when (reject && exception.InnerException is InvalidDataException)
        {
            File.WriteAllText(report, JsonSerializer.Serialize(new { VerificationPassed=true, UnqualifiedRuntimeRejected=true,
                ProductAssemblyHash=Hash(gui.Location), StateHash=Hash(statePath), LiveGuiQualified=false }));
            return;
        }
        using var disposable = (IDisposable)session;
        if (reject) throw new InvalidDataException("Unqualified package accepted factory");
        var sessionContract = core.GetType("SekaiToolsCore.Abstractions.IVideoCaptureSession", true)!;
        if ((string)sessionContract.GetProperty("Identity")!.GetValue(session)! != identity) throw new InvalidDataException("Saved identity differs");
        var capture = sessionContract.GetProperty("Capture")!.GetValue(session)!;
        var emgu = capture.GetType().Assembly;
        var matType = emgu.GetType("Emgu.CV.Mat", true)!;
        using var mat = (IDisposable)Activator.CreateInstance(matType)!;
        var read = capture.GetType().GetMethod("Read", [matType])!;
        var channels = matType.GetProperty("NumberOfChannels")!;
        for (var frame = 1; frame <= interval + 1; frame++)
            if (!(bool)read.Invoke(capture, [mat])! || (int)channels.GetValue(mat)! != (frame % interval == 0 ? 3 : 1))
                throw new InvalidDataException("Published factory frame channels differ");
        disposable.Dispose();
        if ((IntPtr)capture.GetType().GetProperty("Ptr")!.GetValue(capture)! != IntPtr.Zero) throw new InvalidDataException("Capture not released");
        var native = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
            .Single(m => m.ModuleName.Equals("cvextern.dll", StringComparison.OrdinalIgnoreCase)).FileName;
        if (Hash(native) != identity.Split(':')[5] || Hash(video) != before ||
            !native.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Runtime or input differs");
        if (!emgu.Location.StartsWith(Path.Combine(AppContext.BaseDirectory,"libs") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Emgu outside published libs");
        File.WriteAllText(report, JsonSerializer.Serialize(new { VerificationPassed=true, SavedIdentitySelectedFactory=true,
            CaptureIdentity=identity, CaptureDisposed=true, FramesChecked=interval+1, NativePath=native, NativeHash=Hash(native),
            ProductAssemblyHash=Hash(gui.Location), StateHash=Hash(statePath), InputHash=before, LiveGuiQualified=false }));
    }

    private static void TimelineAudit(Assembly gui, string video, string report)
    {
        if (File.Exists(report)) throw new IOException("Do not overwrite evidence");
        var inputHash = Hash(video);
        var type = gui.GetType("SekaiToolsGUI.View.Subtitle.Components.TimelineFramePreviewLoader", true)!;
        var loader = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [video], null)!;
        var load = type.GetMethod("LoadFrameAsync")!;
        var pair = type.GetMethod("LoadPairAsync")!;
        static object? Await(object value)
        {
            var task = (Task)value;
            task.GetAwaiter().GetResult();
            return task.GetType().GetProperty("Result")!.GetValue(task);
        }
        static object Pixels(BitmapSource source)
        {
            if (!source.IsFrozen) throw new InvalidDataException("Timeline image is not frozen");
            var stride = (source.PixelWidth * source.Format.BitsPerPixel + 7) / 8;
            var bytes = new byte[stride * source.PixelHeight];
            source.CopyPixels(bytes, stride, 0);
            return new { Width = source.PixelWidth, Height = source.PixelHeight, Format = source.Format.ToString(),
                Hash = Convert.ToHexString(SHA256.HashData(bytes)) };
        }
        var rows = new List<object>();
        try
        {
            foreach (var index in new[] { 0, 1, 1028, 1029, 1030, 100, 50 })
            {
                var bitmap = (BitmapSource?)Await(load.Invoke(loader, [index, CancellationToken.None])!)
                    ?? throw new InvalidDataException("Timeline frame unavailable");
                rows.Add(new { FrameIndex = index, Image = Pixels(bitmap) });
            }
            foreach (var indices in new[] { (1028, 1029), (1029, 1029) })
            {
                var result = Await(pair.Invoke(loader, [indices.Item1, indices.Item2, CancellationToken.None])!)!;
                var first = (BitmapSource)result.GetType().GetField("Item1")!.GetValue(result)!;
                var second = (BitmapSource)result.GetType().GetField("Item2")!.GetValue(result)!;
                if (indices.Item1 == indices.Item2 && !ReferenceEquals(first, second)) throw new InvalidDataException("Duplicate pair not reused");
                rows.Add(new { First = indices.Item1, Second = indices.Item2, FirstImage = Pixels(first), SecondImage = Pixels(second) });
            }
            if (Await(load.Invoke(loader, [-1, CancellationToken.None])!) != null) throw new InvalidDataException("Invalid index accepted");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            try { Await(load.Invoke(loader, [0, cancellation.Token])!); throw new InvalidDataException("Cancellation ignored"); }
            catch (OperationCanceledException) { }
        }
        finally
        {
            var capture = type.GetField("_capture", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(loader);
            ((IDisposable)loader).Dispose();
            if (capture != null && (IntPtr)capture.GetType().GetProperty("Ptr")!.GetValue(capture)! != IntPtr.Zero)
                throw new InvalidDataException("Timeline native pointer not released");
        }
        if (type.GetField("_capture", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(loader) != null)
            throw new InvalidDataException("Timeline capture retained after disposal");
        if (Hash(video) != inputHash) throw new InvalidDataException("Input changed");
        var native = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
            .Single(m => m.ModuleName.Equals("cvextern.dll", StringComparison.OrdinalIgnoreCase)).FileName;
        if (!native.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Timeline runtime outside published package");
        File.WriteAllText(report, JsonSerializer.Serialize(new { Video = Path.GetFullPath(video), InputHash = inputHash,
            ProductAssemblyHash = Hash(gui.Location), NativePath = native, NativeHash = Hash(native), Images = rows,
            CancellationChecked = true, InvalidFrameChecked = true, CaptureDisposed = true,
            LiveGuiQualified = false }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
