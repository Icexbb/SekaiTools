using System.Diagnostics;

namespace SekaiTools.UITests.Helpers;

public static class WaitHelper
{
    public static T Until<T>(Func<T?> query, string expectation, TimeSpan? timeout = null) where T : class
    {
        var watch = Stopwatch.StartNew();
        Exception? last = null;
        using var pause = new ManualResetEventSlim();
        while (watch.Elapsed < (timeout ?? TimeSpan.FromSeconds(15)))
        {
            try { if (query() is { } result) return result; }
            catch (Exception e) { last = e; }
            pause.Wait(TimeSpan.FromMilliseconds(100));
        }
        throw new TimeoutException($"等待超时：{expectation}；耗时 {watch.Elapsed}；最后错误：{last?.Message}", last);
    }

    public static void Until(Func<bool> condition, string expectation, TimeSpan? timeout = null) =>
        Until(() => condition() ? new object() : null, expectation, timeout);
}
