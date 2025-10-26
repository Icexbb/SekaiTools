using System.IO;
using System.Reflection;
using System.Windows;

namespace Updater;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;
        InitializeComponent();
    }

    private static Assembly? OnResolveAssembly(object? sender, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name).Name + ".dll";
        var libsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libs");
        var dllPath = Path.Combine(libsPath, assemblyName);

        return File.Exists(dllPath) ? Assembly.LoadFrom(dllPath) : null;
    }
}