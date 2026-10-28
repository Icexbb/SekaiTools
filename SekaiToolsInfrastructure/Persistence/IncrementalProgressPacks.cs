using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;

namespace SekaiToolsInfrastructure.Persistence;

internal static partial class IncrementalProgressStore
{
    private const int PackTargetBytes = 1024 * 1024;

    // Only protocol reference slots are visited; active frame arrays are data,
    // never interpreted as references or scanned for a reusable block identity.
    private static IEnumerable<JsonElement> References(JsonElement root)
    {
        foreach (var name in new[] { "Dialog", "Marker", "Banner" })
            if (root.TryGetProperty(name, out var matcher) && matcher.ValueKind == JsonValueKind.Object &&
                matcher.TryGetProperty("FrameSets", out var frames) && frames.ValueKind == JsonValueKind.Array)
                foreach (var item in frames.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.Object) yield return item;
        if (root.TryGetProperty("Timecodes", out var times) && times.ValueKind == JsonValueKind.Object &&
            times.TryGetProperty("$blocks", out var blocks) && blocks.ValueKind == JsonValueKind.Array)
            foreach (var item in blocks.EnumerateArray())
                if (item.ValueKind == JsonValueKind.Object) yield return item;
    }

    private sealed class ChunkWriteBatch : IDisposable
    {
        private readonly string _directory;
        private readonly Dictionary<string, string?> _locations = new(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]> _pending = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<ChunkIdentity>> _waiting = new(StringComparer.Ordinal);
        private readonly ChunkReadSession _reader;
        private readonly Dictionary<object, ChunkIdentity> _snapshot = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<string> _available = new(StringComparer.Ordinal);
        private bool _prepared;
        private long _pendingBytes;

        public ChunkWriteBatch(string manifest)
        {
            _directory = Path.GetFullPath(manifest) + ".chunks";
            _reader = new ChunkReadSession(manifest, true);
            if (!File.Exists(manifest)) return;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllBytes(manifest));
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("CheckpointStorage", out var storage) ||
                    storage.ValueKind != JsonValueKind.Number || !storage.TryGetInt32(out var format) || format is not (1 or 2)) return;
                foreach (var reference in References(root))
                {
                    if (!reference.TryGetProperty("$chunk", out var chunk) || chunk.ValueKind != JsonValueKind.String || !IsHash(chunk.GetString())) continue;
                    var hash = chunk.GetString()!;
                    if (!reference.TryGetProperty("$pack", out var pack)) _locations[hash] = null;
                    else if (pack.ValueKind == JsonValueKind.String && IsHash(pack.GetString())) _locations[hash] = pack.GetString();
                }
            }
            catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
            {
                // The current DTO is authoritative. An unreadable prior index
                // only prevents reuse; it must not supply unchecked paths.
                Logger.Log($"检查点块索引无法复用，将从当前快照重建: {error.Message}", LogLevel.Warning);
            }
        }

        public ChunkIdentity Add<T>(T value, bool reuse) where T : class
        {
            // Preparation has already published and checked these dependencies.
            // The streaming pass only needs their immutable reference metadata.
            if (_prepared && _snapshot.TryGetValue(value, out var prepared)) return prepared;
            byte[]? bytes = null;
            if (!_snapshot.TryGetValue(value, out var identity))
            {
                ChunkIdentity Create(object item)
                {
                    bytes = JsonSerializer.SerializeToUtf8Bytes((T)item);
                    return new ChunkIdentity(Convert.ToHexString(SHA256.HashData(bytes)));
                }
                identity = reuse ? Identities.GetValue(value, Create) : Create(value);
                _snapshot.Add(value, identity);
            }
            Add(identity, () => bytes ?? JsonSerializer.SerializeToUtf8Bytes(value));
            return identity;
        }

        public void Add(ChunkIdentity identity, Func<byte[]> payload)
        {
            var hash = identity.Hash;
            if (_pending.ContainsKey(hash)) { _waiting[hash].Add(identity); return; }
            var trusted = identity.Location;
            if (trusted != null && trusted.Directory == _directory && Exists(trusted.Pack ?? hash))
            {
                _locations[hash] = trusted.Pack;
                return;
            }
            if (_locations.TryGetValue(hash, out var pack) && Exists(pack ?? hash) && VerifyExisting(identity, pack)) return;
            _locations.Remove(hash);
            // Version 1 chunks remain valid and need no migration rewrite.
            if (Exists(hash) && VerifyExisting(identity, null)) return;
            var bytes = payload();
            if (bytes.Length > PackTargetBytes)
            {
                // A large single payload already has one durable file; wrapping
                // it in another large JSON object only adds memory copies.
                Flush();
                WritePayload(hash, bytes);
                _locations[hash] = null;
                identity.Location = new ChunkLocation(_directory, null);
                return;
            }
            if (_pending.Count > 0 && _pendingBytes + bytes.Length > PackTargetBytes) Flush();
            _pending.Add(hash, bytes);
            _waiting.Add(hash, [identity]);
            _pendingBytes += bytes.Length;
            if (_pendingBytes >= PackTargetBytes || _prepared) Flush();
        }

        private bool VerifyExisting(ChunkIdentity identity, string? pack)
        {
            try
            {
                _reader.Read(identity.Hash, pack);
                identity.Location = new ChunkLocation(_directory, pack);
                _locations[identity.Hash] = pack;
                return true;
            }
            catch (Exception error) when (error is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
            {
                Logger.Log($"已有检查点块索引校验失败，将由当前快照重建: {identity.Hash}, {error.Message}", LogLevel.Warning);
                return false;
            }
        }

        private bool Exists(string hash)
        {
            // Share this existence check between members of the same immutable
            // pack, only within this save. Missing files are never memoized.
            if (_available.Contains(hash)) return true;
            if (!Directory.Exists(_directory)) return false;
            CheckDirectory(_directory);
            var path = Path.Combine(_directory, hash + ".json");
            if (!File.Exists(path)) return false;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Redirected checkpoint payload");
            _available.Add(hash);
            return true;
        }

        private void Flush()
        {
            if (_pending.Count == 0) return;
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteStartObject("$chunks");
                foreach (var (hash, bytes) in _pending)
                {
                    writer.WritePropertyName(hash);
                    writer.WriteRawValue(bytes, skipInputValidation: true);
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            var packed = buffer.ToArray();
            var identity = Convert.ToHexString(SHA256.HashData(packed));
            WritePayload(identity, packed);
            foreach (var hash in _pending.Keys)
            {
                _locations[hash] = identity;
                foreach (var waiting in _waiting[hash]) waiting.Location = new ChunkLocation(_directory, identity);
            }
            _pending.Clear();
            _waiting.Clear();
            _pendingBytes = 0;
        }

        private void WritePayload(string identity, byte[] bytes)
        {
            Directory.CreateDirectory(_directory);
            CheckDirectory(_directory);
            var path = Path.Combine(_directory, identity + ".json");
            if (!Exists(identity) || Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) != identity)
                WriteAtomic(path, bytes);
            _available.Add(identity);
        }

        public void Dispose() => _reader.Dispose();

        public void CompletePreparation()
        {
            Flush();
            _prepared = true;
        }

        public string? PackFor(string hash) => _locations[hash];
    }

    private sealed class ChunkReadSession(string manifest, bool allowPacks) : IDisposable
    {
        private readonly string _directory = Path.GetFullPath(manifest) + ".chunks";
        private readonly Dictionary<string, JsonDocument> _packs = new(StringComparer.Ordinal);

        private byte[] ReadFile(string hash)
        {
            CheckDirectory(_directory);
            var path = Path.Combine(_directory, hash + ".json");
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Redirected checkpoint payload");
            var bytes = File.ReadAllBytes(path);
            if (Convert.ToHexString(SHA256.HashData(bytes)) != hash)
                throw new InvalidDataException("Checkpoint payload checksum mismatch");
            return bytes;
        }

        public byte[] Read(string hash, string? pack)
        {
            if (pack == null) return ReadFile(hash);
            if (!allowPacks) throw new InvalidDataException("Packs require checkpoint storage version 2");
            if (!_packs.TryGetValue(pack, out var document))
            {
                document = JsonDocument.Parse(ReadFile(pack));
                _packs.Add(pack, document);
            }
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("$chunks", out var chunks) || chunks.ValueKind != JsonValueKind.Object ||
                !chunks.TryGetProperty(hash, out var value)) throw new InvalidDataException("Missing packed checkpoint chunk");
            var bytes = Encoding.UTF8.GetBytes(value.GetRawText());
            if (Convert.ToHexString(SHA256.HashData(bytes)) != hash)
                throw new InvalidDataException("Packed checkpoint chunk checksum mismatch");
            return bytes;
        }

        public void Dispose()
        {
            foreach (var document in _packs.Values) document.Dispose();
            _packs.Clear();
        }
    }
}
