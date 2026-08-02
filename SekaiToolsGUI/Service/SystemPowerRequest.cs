using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using SekaiToolsBase;

namespace SekaiToolsGUI.Service;

/// <summary>
/// Keeps Windows and the display awake for the lifetime of the returned lease.
/// </summary>
internal sealed class SystemPowerRequest : IDisposable
{
    private SafeFileHandle? _handle;
    private bool _displayRequired;
    private bool _systemRequired;

    private SystemPowerRequest()
    {
    }

    public static IDisposable Acquire(string reason)
    {
        try
        {
            var request = new SystemPowerRequest();
            if (request.TryActivate(reason))
                return request;

            request.Dispose();
        }
        catch (Exception e)
        {
            Logger.Log($"创建系统电源请求失败: {e.Message}", LogLevel.Warning);
        }

        return EmptyLease.Instance;
    }

    private bool TryActivate(string reason)
    {
        var context = new ReasonContext
        {
            Version = ReasonContextVersion,
            Flags = ReasonContextSimpleString,
            SimpleReasonString = reason
        };

        _handle = PowerCreateRequest(ref context);
        if (_handle.IsInvalid)
        {
            LogLastWin32Error("创建系统电源请求失败");
            return false;
        }

        if (!PowerSetRequest(_handle, PowerRequestType.SystemRequired))
        {
            LogLastWin32Error("阻止系统睡眠失败");
            return false;
        }

        _systemRequired = true;
        if (!PowerSetRequest(_handle, PowerRequestType.DisplayRequired))
        {
            LogLastWin32Error("阻止显示器关闭失败");
            return false;
        }

        _displayRequired = true;
        return true;
    }

    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, null);
        if (handle == null)
            return;

        if (!handle.IsInvalid)
        {
            if (_displayRequired)
                PowerClearRequest(handle, PowerRequestType.DisplayRequired);
            if (_systemRequired)
                PowerClearRequest(handle, PowerRequestType.SystemRequired);
        }

        handle.Dispose();
    }

    private static void LogLastWin32Error(string message)
    {
        var error = new Win32Exception(Marshal.GetLastWin32Error());
        Logger.Log($"{message}: {error.Message}", LogLevel.Warning);
    }

    private const uint ReasonContextVersion = 0;
    private const uint ReasonContextSimpleString = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ReasonContext
    {
        public uint Version;
        public uint Flags;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string SimpleReasonString;
    }

    private enum PowerRequestType
    {
        DisplayRequired,
        SystemRequired
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle PowerCreateRequest(ref ReasonContext context);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerSetRequest(SafeFileHandle powerRequest, PowerRequestType requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerClearRequest(SafeFileHandle powerRequest, PowerRequestType requestType);

    private sealed class EmptyLease : IDisposable
    {
        public static EmptyLease Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
