using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SekaiToolsCore.Process.Model;
using ProcessConfig = SekaiToolsCore.Process.Config.Config;

namespace SekaiToolsCore.Process;

public sealed record FileFingerprint(long Length, long LastWriteTimeUtcTicks, string SampleHash)
{
    private const int SampleSize = 64 * 1024;

    public static FileFingerprint Create(string path)
    {
        var info = new FileInfo(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var firstLength = (int)Math.Min(SampleSize, stream.Length);
        var buffer = new byte[firstLength];
        stream.ReadExactly(buffer);
        hash.AppendData(buffer);

        if (stream.Length > SampleSize)
        {
            stream.Position = Math.Max(0, stream.Length - SampleSize);
            var lastLength = (int)Math.Min(SampleSize, stream.Length - stream.Position);
            buffer = new byte[lastLength];
            stream.ReadExactly(buffer);
            hash.AppendData(buffer);
        }

        hash.AppendData(BitConverter.GetBytes(stream.Length));
        return new FileFingerprint(stream.Length, info.LastWriteTimeUtc.Ticks,
            Convert.ToHexString(hash.GetHashAndReset()));
    }
}

public sealed record VideoStateMetadata(int Width, int Height, int FrameCount, double Fps)
{
    public static VideoStateMetadata From(VideoInfo videoInfo)
    {
        return new VideoStateMetadata(videoInfo.Resolution.Width, videoInfo.Resolution.Height,
            videoInfo.FrameCount, videoInfo.Fps.Fps());
    }
}

public sealed record ProcessingStateMetadata(
    FileFingerprint Video,
    FileFingerprint Script,
    FileFingerprint? Translation,
    VideoStateMetadata VideoInfo,
    string ConfigHash)
{
    public static ProcessingStateMetadata Create(ProcessConfig config, VideoStateMetadata videoInfo)
    {
        var configJson = JsonSerializer.Serialize(new
        {
            config.StyleFontConfig,
            config.ExportStyleConfig,
            config.TyperSetting,
            config.MatchingThreshold
        });
        var configHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(configJson)));
        return new ProcessingStateMetadata(
            FileFingerprint.Create(config.VideoFilePath),
            FileFingerprint.Create(config.ScriptFilePath),
            string.IsNullOrEmpty(config.TranslateFilePath)
                ? null
                : FileFingerprint.Create(config.TranslateFilePath),
            videoInfo,
            configHash);
    }
}

public enum ProcessingStateCompatibilityStatus
{
    Compatible,
    MigratedLegacy,
    Incompatible
}

public sealed record ProcessingStateCompatibilityResult(
    ProcessingStateCompatibilityStatus Status,
    string Message)
{
    public bool CanRestore => Status != ProcessingStateCompatibilityStatus.Incompatible;
}

public static class ProcessingStateCompatibility
{
    public const string CurrentVersion = "2.0";

    public static ProcessingStateCompatibilityResult ValidateAndMigrate(
        ProcessingState state,
        ProcessConfig config,
        ProcessingStateMetadata currentMetadata)
    {
        if (state.Version == "1.0")
        {
            if (!PathsMatch(state, config))
                return new ProcessingStateCompatibilityResult(
                    ProcessingStateCompatibilityStatus.Incompatible, "旧版进度的输入文件路径与当前任务不一致");

            state.Version = CurrentVersion;
            state.Metadata = currentMetadata;
            return new ProcessingStateCompatibilityResult(
                ProcessingStateCompatibilityStatus.MigratedLegacy,
                "已从 1.0 进度迁移；旧格式不含内容指纹，仅完成了路径级校验");
        }

        if (state.Version != CurrentVersion)
            return new ProcessingStateCompatibilityResult(
                ProcessingStateCompatibilityStatus.Incompatible, $"不支持的进度版本: {state.Version}");

        if (!PathsMatch(state, config))
            return new ProcessingStateCompatibilityResult(
                ProcessingStateCompatibilityStatus.Incompatible, "进度中的输入文件路径与当前任务不一致");
        if (state.Metadata == null)
            return new ProcessingStateCompatibilityResult(
                ProcessingStateCompatibilityStatus.Incompatible, "进度缺少输入指纹元数据");
        if (state.Metadata != currentMetadata)
            return new ProcessingStateCompatibilityResult(
                ProcessingStateCompatibilityStatus.Incompatible, "输入文件、视频元数据或处理配置已发生变化");

        return new ProcessingStateCompatibilityResult(
            ProcessingStateCompatibilityStatus.Compatible, "进度状态兼容");
    }

    private static bool PathsMatch(ProcessingState state, ProcessConfig config)
    {
        return SamePath(state.VideoFilePath, config.VideoFilePath) &&
               SamePath(state.ScriptFilePath, config.ScriptFilePath) &&
               SamePath(state.TranslateFilePath, config.TranslateFilePath);
    }

    private static bool SamePath(string left, string right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            return string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right);
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }
}
