using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using SekaiToolsCore.Process;

namespace SekaiToolsInfrastructure.Persistence;

/// <summary>Immutable completed events are written once; the manifest is committed last.</summary>
internal static class IncrementalProgressStore
{
    private const string VersionPrefix = "chunks1:";
    private sealed record ChunkIdentity(string Hash);
    private static readonly ConditionalWeakTable<object, ChunkIdentity> Identities = new();

    public static void Save(string path, ProcessingState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, Options(path, state.ReusesCompletedFrames,
            timecodeBlocks: state.CheckpointTimecodeBlocks));
        WriteAtomic(path, bytes);
    }

    public static ProcessingState Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid checkpoint root");
        if (!document.RootElement.TryGetProperty("CheckpointStorage", out var format))
            return document.Deserialize<ProcessingState>(Options(path, allowChunks: false)) ?? throw new InvalidDataException("Missing processing state");
        if (format.ValueKind != JsonValueKind.Number || !format.TryGetInt32(out var version) || version != 1)
            throw new InvalidDataException("Unsupported checkpoint storage format");
        var state = document.Deserialize<ProcessingState>(Options(path)) ?? throw new InvalidDataException("Missing processing state");
        if (!state.Version.StartsWith(VersionPrefix, StringComparison.Ordinal))
            throw new InvalidDataException("Missing chunked checkpoint version marker");
        state.Version = state.Version[VersionPrefix.Length..];
        return state;
    }

    public static void Delete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        var chunks = Path.GetFullPath(path) + ".chunks";
        if (!Directory.Exists(chunks)) return;
        try
        {
            CheckDirectory(chunks);
            // Only owned digest-named files are removed; never recursively follow subdirectories.
            foreach (var file in Directory.EnumerateFiles(chunks, "*.json"))
            {
                CheckDirectory(chunks);
                if (IsHash(Path.GetFileNameWithoutExtension(file)) &&
                        (File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) File.Delete(file);
            }
            if (!Directory.EnumerateFileSystemEntries(chunks).Any()) Directory.Delete(chunks);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Logger.Log($"检查点已移除，分块清理未完成: {chunks}, {exception.Message}", LogLevel.Warning);
        }
    }

    private static JsonSerializerOptions Options(string path, bool reuseCompleted = false, bool allowChunks = true,
        IReadOnlyList<int[]>? timecodeBlocks = null)
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != typeof(ProcessingState)) return;
            // Older readers must reject this representation instead of treating chunk references as empty frames.
            info.Properties.Single(property => property.Name == nameof(ProcessingState.Version)).Get =
                value => VersionPrefix + ((ProcessingState)value).Version;
            info.Properties.Single(property => property.Name == nameof(ProcessingState.Timecodes)).CustomConverter =
                new TimecodeConverter(path, timecodeBlocks, allowChunks, reuseCompleted && timecodeBlocks != null);
            var storage = info.CreateJsonPropertyInfo(typeof(int), "CheckpointStorage");
            storage.Get = _ => 1;
            info.Properties.Add(storage);
        });
        var options = new JsonSerializerOptions { TypeInfoResolver = resolver };
        options.Converters.Add(new EventConverter<DialogFrameSetDto>(path, item => item.Finished, reuseCompleted, allowChunks));
        options.Converters.Add(new EventConverter<MarkerFrameSetDto>(path, item => item.Finished, reuseCompleted, allowChunks));
        options.Converters.Add(new EventConverter<BannerFrameSetDto>(path, item => item.Finished, reuseCompleted, allowChunks));
        return options;
    }

    private sealed class TimecodeConverter(string path, IReadOnlyList<int[]>? frozenBlocks, bool allowChunks, bool reuseFrozen) : JsonConverter<List<int>>
    {
        private readonly EventConverter<int[]> _blocks = new(path, _ => true, reuseFrozen, allowChunks);

        public override List<int> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.StartArray)
                return JsonSerializer.Deserialize<List<int>>(ref reader) ?? [];
            using var document = JsonDocument.ParseValue(ref reader);
            if (!allowChunks || document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("$blocks", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Invalid checkpoint timecode blocks");
            var result = new List<int>();
            foreach (var block in blocks.EnumerateArray())
            {
                var blockReader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(block.GetRawText()));
                blockReader.Read();
                var values = _blocks.Read(ref blockReader, typeof(int[]), options);
                if (values.Length is < 1 or > 4096) throw new InvalidDataException("Invalid timecode block length");
                result.AddRange(values);
            }
            return result;
        }

        public override void Write(Utf8JsonWriter writer, List<int> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteStartArray("$blocks");
            foreach (var block in frozenBlocks ?? value.Chunk(4096))
            {
                if (block.Length is < 1 or > 4096) throw new InvalidDataException("Invalid timecode block length");
                _blocks.Write(writer, block, options);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }

    private sealed class EventConverter<T>(string manifest, Func<T, bool> completed, bool reuseCompleted, bool allowChunks) : JsonConverter<T> where T : class
    {
        private readonly string _directory = Path.GetFullPath(manifest) + ".chunks";

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid checkpoint event");
            if (!document.RootElement.TryGetProperty("$chunk", out var reference))
                return document.Deserialize<T>() ?? throw new InvalidDataException("Missing checkpoint event");
            if (!allowChunks || reference.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("Invalid checkpoint chunk reference");
            var hash = reference.GetString();
            if (!IsHash(hash)) throw new InvalidDataException("Invalid checkpoint chunk reference");
            CheckDirectory(_directory);
            var path = Path.Combine(_directory, hash + ".json");
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Redirected checkpoint chunk");
            var bytes = File.ReadAllBytes(path);
            if (Convert.ToHexString(SHA256.HashData(bytes)) != hash)
                throw new InvalidDataException("Checkpoint chunk checksum mismatch");
            var value = JsonSerializer.Deserialize<T>(bytes) ?? throw new InvalidDataException("Empty checkpoint chunk");
            if (!completed(value)) throw new InvalidDataException("Chunk is not a completed event");
            return value;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (!completed(value)) { JsonSerializer.Serialize(writer, value); return; }
            byte[]? bytes = null;
            ChunkIdentity CreateIdentity(object item)
            {
                bytes = JsonSerializer.SerializeToUtf8Bytes((T)item);
                return new ChunkIdentity(Convert.ToHexString(SHA256.HashData(bytes)));
            }
            var identity = reuseCompleted ? Identities.GetValue(value, CreateIdentity) : CreateIdentity(value);
            Directory.CreateDirectory(_directory);
            CheckDirectory(_directory);
            var path = Path.Combine(_directory, identity.Hash + ".json");
            if (!File.Exists(path)) WriteAtomic(path, bytes ?? JsonSerializer.SerializeToUtf8Bytes(value));
            writer.WriteStartObject();
            writer.WriteString("$chunk", identity.Hash);
            writer.WriteEndObject();
        }
    }

    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static void CheckDirectory(string directory)
    {
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Redirected checkpoint chunk directory");
    }

    private static void WriteAtomic(string path, byte[] bytes)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
