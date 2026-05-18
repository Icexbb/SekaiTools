using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SekaiToolsGUI;

internal static class AssemblyResolver
{
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
            Debug.WriteLine($"[AssemblyResolver] Not found: {args.Name}");
            return null;
        }

        Debug.WriteLine($"[AssemblyResolver] Loading from libs: {assemblyName}");
        return Assembly.LoadFrom(dllPath);
    }
}
