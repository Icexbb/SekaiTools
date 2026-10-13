using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SekaiToolsGUI;

internal static class AssemblyResolver
{
    // Resolving Logger's own dependencies must never invoke Logger recursively.
    private static readonly ConcurrentQueue<string> Diagnostics = new();

    internal static void FlushDiagnostics()
    {
        while (Diagnostics.TryDequeue(out var message))
            SekaiToolsBase.Logger.Log(message, callerMemberName: nameof(AssemblyResolver));
    }

    [ModuleInitializer]
    internal static void Initialize()
    {
        AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
    }

    private static Assembly? ResolveAssembly(object? sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name);
        // if (name.Name is { } n && (n.EndsWith(".resources") || n.EndsWith(".XmlSerializers")))
        //     return null;

        var assemblyName = name.Name + ".dll";
        var libsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libs");
        var dllPath = Path.Combine(libsPath, assemblyName);

        if (!File.Exists(dllPath))
        {
            // Missing satellite assemblies are normal probing, not application failures.
            if (name.Name is { } missing && !missing.EndsWith(".resources") && !missing.EndsWith(".XmlSerializers"))
                Diagnostics.Enqueue($"程序集未找到: {args.Name}");
            return null;
        }

        Diagnostics.Enqueue($"从 libs 加载程序集: {assemblyName}");
        return Assembly.LoadFrom(dllPath);
    }
}
