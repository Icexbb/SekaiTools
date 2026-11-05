using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Updater;

internal sealed record UpdateFile(string Path, bool HadOriginal);

internal static class UpdateInstallation
{
    internal const string Journal = "install.json";
    private const string TrialProcess = "trial.json";
    private sealed record ProcessIdentity(int Id, long StartTicks);

    internal static void Run(string install, string work, string? stage, int parentId, long parentStartTicks)
    {
        using var updateLock = UpdatePaths.AcquireLock(work);
        using var parent = FindProcess(new ProcessIdentity(parentId, parentStartTicks));
        if (parent is not null && !parent.WaitForExit(120000))
            throw new TimeoutException("主程序尚未退出，已取消安装");

        var journal = Path.Combine(work, UpdatePaths.Journal);
        var backup = Path.Combine(work, "backup");
        var trial = Path.Combine(work, TrialProcess);
        if (File.Exists(journal))
        {
            StopTrial(trial);
            Restore(install, backup, Read<UpdateFile[]>(journal));
            File.Delete(journal);
            File.Delete(trial);
            if (stage is null)
            {
                DeleteDirectory(backup);
                using var recovered = StartMain(install);
                return;
            }
        }

        if (stage is null) return;
        var source = Path.Combine(stage, "SekaiTools");
        HashSet<string> next;
        UpdateFile[] files;
        try
        {
            next = ReadManifest(source);
            if (!next.Contains(UpdatePaths.MainExecutable))
                throw new InvalidDataException("更新包清单缺少主程序");
            foreach (var path in next)
            {
                if (!File.Exists(Inside(source, path)))
                    throw new InvalidDataException($"更新包缺少文件：{path}");
            }

            var previous = ReadManifest(install);
            previous.UnionWith(["Updater.exe", "7zr.exe"]);
            previous.UnionWith(next);

            DeleteDirectory(backup);
            File.Delete(trial);
            files = previous.Select(path => new UpdateFile(path, File.Exists(Inside(install, path)))).ToArray();
            foreach (var file in files.Where(file => file.HadOriginal))
                Copy(Inside(install, file.Path), Inside(backup, file.Path));

            Write(journal, files);
        }
        catch
        {
            using var recovered = StartMain(install);
            throw;
        }

        try
        {
            foreach (var file in files)
            {
                var destination = Inside(install, file.Path);
                if (next.Contains(file.Path))
                    Replace(Inside(source, file.Path), destination);
                else
                    File.Delete(destination);
            }

            var eventName = @"Local\SekaiTools-Update-" + Guid.NewGuid().ToString("N");
            using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);
            using var process = StartMain(install, eventName);
            try
            {
                Write(trial, new ProcessIdentity(process.Id, process.StartTime.ToUniversalTime().Ticks));
                var timer = Stopwatch.StartNew();
                while (!ready.WaitOne(200))
                {
                    if (process.HasExited || timer.Elapsed > TimeSpan.FromSeconds(90))
                        throw new InvalidOperationException("新版未能完成启动");
                }

                File.Delete(journal);
            }
            catch
            {
                Stop(process);
                throw;
            }
        }
        catch (Exception installError)
        {
            try
            {
                Restore(install, backup, files);
            }
            catch (Exception restoreError)
            {
                throw new AggregateException("安装失败，恢复旧版也未完成，已保留恢复记录和备份。", installError,
                    restoreError);
            }

            File.Delete(journal);
            File.Delete(trial);
            DeleteDirectory(backup);
            using var recovered = StartMain(install);
            throw;
        }

        File.Delete(trial);
        DeleteDirectory(backup);
    }

    private static HashSet<string> ReadManifest(string directory)
    {
        var manifest = Path.Combine(directory, UpdatePaths.Manifest);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(manifest)) return paths;
        foreach (var path in File.ReadLines(manifest))
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            var normalized = Path.GetRelativePath(directory, Inside(directory, path));
            paths.Add(normalized);
        }

        paths.Add(UpdatePaths.Manifest);
        return paths;
    }

    private static string Inside(string root, string path)
    {
        if (Path.IsPathRooted(path) || path.Contains(':'))
            throw new InvalidDataException("更新清单路径无效");
        var full = Path.GetFullPath(Path.Combine(root, path));
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("更新清单路径超出目录");
        for (var entry = full; !string.Equals(entry, prefix.TrimEnd(Path.DirectorySeparatorChar),
                 StringComparison.OrdinalIgnoreCase); entry = Path.GetDirectoryName(entry)!)
        {
            if ((File.Exists(entry) || Directory.Exists(entry)) &&
                File.GetAttributes(entry).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException("更新目录包含重解析点");
        }

        return full;
    }

    private static void Restore(string install, string backup, IEnumerable<UpdateFile> files)
    {
        foreach (var file in files)
        {
            var destination = Inside(install, file.Path);
            if (file.HadOriginal)
                Replace(Inside(backup, file.Path), destination);
            else
                File.Delete(destination);
        }
    }

    private static void Copy(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var input = File.OpenRead(source);
        using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        input.CopyTo(output);
        output.Flush(flushToDisk: true);
    }

    internal static void Replace(string source, string destination)
    {
        var temporary = destination + ".sekaitools-update";
        var timer = Stopwatch.StartNew();
        try
        {
            while (true)
            {
                try
                {
                    if (File.Exists(destination))
                    {
                        using var original = File.OpenRead(source);
                        using var current = File.OpenRead(destination);
                        if (SHA256.HashData(original).AsSpan().SequenceEqual(SHA256.HashData(current))) return;
                    }

                    Copy(source, temporary);
                    File.Move(temporary, destination, overwrite: true);
                    break;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException &&
                                           (ex.HResult & 0xFFFF) is 5 or 32 or 33 &&
                                           timer.Elapsed < TimeSpan.FromSeconds(15))
                {
                    Thread.Sleep(200);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"无法替换文件 {destination}", ex);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static T Read<T>(string path) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? throw new InvalidDataException("更新记录无效");

    private static void Write<T>(string path, T value)
    {
        var temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, value);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temporary, path, overwrite: true);
    }

    private static Process? FindProcess(ProcessIdentity identity)
    {
        if (identity.Id <= 0) return null;
        Process? process = null;
        try
        {
            process = Process.GetProcessById(identity.Id);
            if (process.StartTime.ToUniversalTime().Ticks == identity.StartTicks) return process;
        }
        catch (ArgumentException)
        {
            // The process already exited.
        }
        catch (InvalidOperationException)
        {
            // The process exited while reading its start time.
        }

        process?.Dispose();
        return null;
    }

    private static void StopTrial(string path)
    {
        if (!File.Exists(path)) return;
        using var process = FindProcess(Read<ProcessIdentity>(path));
        if (process is not null) Stop(process);
    }

    private static void Stop(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill();
            process.WaitForExit();
        }
    }

    private static Process StartMain(string install, string? ready = null)
    {
        var start = new ProcessStartInfo(Path.Combine(install, UpdatePaths.MainExecutable))
        {
            UseShellExecute = false,
            WorkingDirectory = install
        };
        start.Environment[UpdatePaths.SkipUpdate] = "1";
        if (ready is not null) start.Environment[UpdatePaths.ReadyEvent] = ready;
        return Process.Start(start) ?? throw new InvalidOperationException("无法启动主程序");
    }

    internal static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
