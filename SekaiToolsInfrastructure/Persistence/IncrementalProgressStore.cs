using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using SekaiToolsCore.Process;

namespace SekaiToolsInfrastructure.Persistence;

/// <summary>Completed event payloads are immutable; dependencies precede the atomic manifest commit.</summary>
internal static partial class IncrementalProgressStore
{
    private const string VersionPrefix = "chunks2:";
    private sealed record ChunkLocation(string Directory, string? Pack);
    private sealed class ChunkIdentity(string hash)
    {
        public string Hash { get; } = hash;
        public volatile ChunkLocation? Location;
    }
    private static readonly ConditionalWeakTable<object, ChunkIdentity> Identities = new();

    internal const int InlineLimitBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions InlineJsonOptions = CreateInlineOptions();

    public static void Save(string path, ProcessingState state)
    {
        // Even zero-valued coordinates need more than 20 JSON bytes per row;
        // integers need at least one digit and a separator. This lower bound
        // skips speculative work only when inline storage is impossible.
        var rows = (state.Dialog?.FrameSets.Sum(item => (long)item.Frames.Count) ?? 0) +
            (state.Marker?.FrameSets.Sum(item => (long)item.Frames.Count) ?? 0);
        var times = state.CheckpointTimecodeBlocks?.Sum(block => (long)block.Length) ?? state.Timecodes.Count;
        if (rows * 20 + times * 2 > InlineLimitBytes) { SaveChunked(path, state); return; }
        // Bound the speculative full serialization for uncertain sizes.
        using var inline = new LimitedInlineStream();
        try
        {
            using (var writer = new Utf8JsonWriter(inline))
            {
                JsonSerializer.Serialize(writer, state, InlineJsonOptions);
                writer.Flush();
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            WriteAtomic(path, inline.ToArray());
        }
        catch (InlineLimitExceededException) { SaveChunked(path, state); }
    }

    public static void SaveChunked(string path, ProcessingState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var batch = new ChunkWriteBatch(path);
        IReadOnlyList<int[]> blocks = state.CheckpointTimecodeBlocks ?? state.Timecodes.Chunk(4096).ToArray();
        foreach (var block in blocks)
        {
            if (block.Length is < 1 or > 4096) throw new InvalidDataException("Invalid timecode block length");
            batch.Add(block, state.ReusesCompletedFrames && state.CheckpointTimecodeBlocks != null);
        }
        if (state.Dialog != null)
            foreach (var item in state.Dialog.FrameSets.Where(item => item.Finished)) batch.Add(item, state.ReusesCompletedFrames);
        if (state.Marker != null)
            foreach (var item in state.Marker.FrameSets.Where(item => item.Finished)) batch.Add(item, state.ReusesCompletedFrames);
        if (state.Banner != null)
            foreach (var item in state.Banner.FrameSets.Where(item => item.Finished)) batch.Add(item, state.ReusesCompletedFrames);
        batch.CompletePreparation();
        WriteJsonAtomic(path, writer => JsonSerializer.Serialize(writer, state,
            Options(state.ReusesCompletedFrames, timecodeBlocks: blocks, batch: batch)));
    }

    private sealed class InlineLimitExceededException : Exception { }

    private sealed class LimitedInlineStream : Stream
    {
        private readonly MemoryStream _buffer = new();
        public byte[] ToArray() => _buffer.ToArray();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _buffer.Length;
        public override long Position { get => _buffer.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (Length + buffer.Length > InlineLimitBytes) throw new InlineLimitExceededException();
            _buffer.Write(buffer);
        }
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        protected override void Dispose(bool disposing)
        {
            if (disposing) _buffer.Dispose();
            base.Dispose(disposing);
        }
    }

    private static JsonSerializerOptions CreateInlineOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != typeof(ProcessingState)) return;
            var property = info.Properties.Single(item => item.Name == nameof(ProcessingState.Timecodes));
            var index = info.Properties.IndexOf(property);
            // A lazy enumerable preserves the existing DTO API and JSON array
            // shape while sharing serializer metadata across all snapshots.
            var flat = info.CreateJsonPropertyInfo(typeof(IEnumerable<int>), property.Name);
            flat.Get = value => FlatTimecodes((ProcessingState)value);
            info.Properties[index] = flat;
        });
        return new JsonSerializerOptions { TypeInfoResolver = resolver };
    }

    private static IEnumerable<int> FlatTimecodes(ProcessingState state)
    {
        if (state.CheckpointTimecodeBlocks == null)
        {
            foreach (var time in state.Timecodes) yield return time;
        }
        else
        {
            foreach (var block in state.CheckpointTimecodeBlocks)
            {
                if (block.Length is < 1 or > 4096) throw new InvalidDataException("Invalid timecode block length");
                foreach (var time in block) yield return time;
            }
        }
    }

    public static ProcessingState Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid checkpoint root");
        if (!document.RootElement.TryGetProperty("CheckpointStorage", out var format))
        {
            using var legacy = new ChunkReadSession(path, false);
            return document.Deserialize<ProcessingState>(Options(allowChunks: false, readSession: legacy)) ?? throw new InvalidDataException("Missing processing state");
        }
        if (format.ValueKind != JsonValueKind.Number || !format.TryGetInt32(out var version) || version is not (1 or 2))
            throw new InvalidDataException("Unsupported checkpoint storage format");
        using var session = new ChunkReadSession(path, version == 2);
        var state = document.Deserialize<ProcessingState>(Options(readSession: session)) ?? throw new InvalidDataException("Missing processing state");
        var prefix = version == 1 ? "chunks1:" : VersionPrefix;
        if (!state.Version.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidDataException("Missing chunked checkpoint version marker");
        state.Version = state.Version[prefix.Length..];
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

    private static JsonSerializerOptions Options(bool reuseCompleted = false, bool allowChunks = true,
        IReadOnlyList<int[]>? timecodeBlocks = null, ChunkWriteBatch? batch = null, ChunkReadSession? readSession = null)
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != typeof(ProcessingState)) return;
            // Older readers must reject this representation instead of treating chunk references as empty frames.
            info.Properties.Single(property => property.Name == nameof(ProcessingState.Version)).Get =
                value => VersionPrefix + ((ProcessingState)value).Version;
            info.Properties.Single(property => property.Name == nameof(ProcessingState.Timecodes)).CustomConverter =
                new TimecodeConverter(timecodeBlocks, allowChunks, reuseCompleted && timecodeBlocks != null, batch, readSession);
            var storage = info.CreateJsonPropertyInfo(typeof(int), "CheckpointStorage");
            storage.Get = _ => 2;
            info.Properties.Add(storage);
        });
        var options = new JsonSerializerOptions { TypeInfoResolver = resolver };
        options.Converters.Add(new EventConverter<DialogFrameSetDto>(item => item.Finished, reuseCompleted, allowChunks, batch, readSession));
        options.Converters.Add(new EventConverter<MarkerFrameSetDto>(item => item.Finished, reuseCompleted, allowChunks, batch, readSession));
        options.Converters.Add(new EventConverter<BannerFrameSetDto>(item => item.Finished, reuseCompleted, allowChunks, batch, readSession));
        return options;
    }

    private sealed class TimecodeConverter(IReadOnlyList<int[]>? frozenBlocks, bool allowChunks, bool reuseFrozen,
        ChunkWriteBatch? batch, ChunkReadSession? readSession) : JsonConverter<List<int>>
    {
        private readonly EventConverter<int[]> _blocks = new(_ => true, reuseFrozen, allowChunks, batch, readSession);

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

    private sealed class EventConverter<T>(Func<T, bool> completed, bool reuseCompleted, bool allowChunks,
        ChunkWriteBatch? batch, ChunkReadSession? readSession) : JsonConverter<T> where T : class
    {

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid checkpoint event");
            if (!document.RootElement.TryGetProperty("$chunk", out var reference))
            {
                if (document.RootElement.TryGetProperty("$pack", out _)) throw new InvalidDataException("Pack reference without chunk identity");
                return document.Deserialize<T>() ?? throw new InvalidDataException("Missing checkpoint event");
            }
            if (!allowChunks || reference.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("Invalid checkpoint chunk reference");
            var hash = reference.GetString();
            if (!IsHash(hash)) throw new InvalidDataException("Invalid checkpoint chunk reference");
            string? pack = null;
            if (document.RootElement.TryGetProperty("$pack", out var packReference))
            {
                if (packReference.ValueKind != JsonValueKind.String || !IsHash(packReference.GetString()))
                    throw new InvalidDataException("Invalid checkpoint pack reference");
                pack = packReference.GetString();
            }
            var bytes = (readSession ?? throw new InvalidOperationException("Missing checkpoint reader")).Read(hash!, pack);
            var value = JsonSerializer.Deserialize<T>(bytes) ?? throw new InvalidDataException("Empty checkpoint chunk");
            if (!completed(value)) throw new InvalidDataException("Chunk is not a completed event");
            return value;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (!completed(value)) { JsonSerializer.Serialize(writer, value); return; }
            var context = batch ?? throw new InvalidOperationException("Missing checkpoint writer");
            var identity = context.Add(value, reuseCompleted);
            writer.WriteStartObject();
            writer.WriteString("$chunk", identity.Hash);
            if (context.PackFor(identity.Hash) is { } pack) writer.WriteString("$pack", pack);
            writer.WriteEndObject();
        }
    }

    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static void CheckDirectory(string directory)
    {
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Redirected checkpoint chunk directory");
    }

    private static void WriteJsonAtomic(string path, Action<Utf8JsonWriter> write)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var writer = new Utf8JsonWriter(stream)) write(writer);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
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
