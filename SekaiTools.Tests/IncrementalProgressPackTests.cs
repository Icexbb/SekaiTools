using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SekaiToolsCore.Process;
using SekaiToolsInfrastructure.Persistence;

namespace SekaiTools.Tests;

public class IncrementalProgressPackTests
{
    [Fact]
    public void OversizedCompletedEventUsesOneUnwrappedContentFile()
    {
        using var fixture = new Fixture();
        var done = new DialogFrameSetDto
        {
            Finished = true,
            Frames = Enumerable.Range(0, 60000).Select(i => new FrameResultDto(i, 1234, 2345)).ToList()
        };
        var state = new ProcessingState { ReusesCompletedFrames = true, Dialog = new() { FrameSets = [done] } };
        var expected = JsonSerializer.SerializeToUtf8Bytes(done);
        IncrementalProgressStore.SaveChunked(fixture.Manifest, state);
        var file = Assert.Single(Directory.GetFiles(fixture.Chunks));
        Assert.Equal(Hash(expected), Path.GetFileNameWithoutExtension(file));
        Assert.Equal(expected, File.ReadAllBytes(file));
        var root = JsonNode.Parse(File.ReadAllBytes(fixture.Manifest))!;
        Assert.Null(root["Dialog"]!["FrameSets"]![0]!["$pack"]);
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(IncrementalProgressStore.Load(fixture.Manifest)));
    }

    [Fact]
    public void LargeActiveTrajectoryStaysInlineAndRestoresWithPackedCompletedEvents()
    {
        using var fixture = new Fixture();
        var active = new DialogFrameSetDto { Frames = Enumerable.Range(0, 40000).Select(i => new FrameResultDto(i, 1234, 2345)).ToList() };
        var state = new ProcessingState
        {
            Dialog = new()
            {
                FrameSets =
            [new() { Finished = true, Frames = [new(0, 1, 2)] }, active]
            }
        };
        IncrementalProgressStore.Save(fixture.Manifest, state);
        Assert.True(new FileInfo(fixture.Manifest).Length > IncrementalProgressStore.InlineLimitBytes);
        Assert.Single(Directory.GetFiles(fixture.Chunks));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(IncrementalProgressStore.Load(fixture.Manifest)));
    }

    [Fact]
    public void InterruptedStreamingManifestLeavesPriorFileAndRemovesTemporaryFile()
    {
        using var fixture = new Fixture();
        IncrementalProgressStore.SaveChunked(fixture.Manifest, new ProcessingState { FrameIndex = 10 });
        var prior = File.ReadAllBytes(fixture.Manifest);
        var write = typeof(IncrementalProgressStore).GetMethod("WriteJsonAtomic",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .CreateDelegate<Action<string, Action<Utf8JsonWriter>>>();
        Assert.Throws<InvalidOperationException>(() => write(fixture.Manifest, writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("FrameIndex", 20);
            throw new InvalidOperationException("Injected serialization failure");
        }));
        Assert.Equal(prior, File.ReadAllBytes(fixture.Manifest));
        Assert.Equal(10, IncrementalProgressStore.Load(fixture.Manifest).FrameIndex);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(fixture.Manifest)!, "*.tmp"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WrongExistingPackBindingCannotContaminateNewCheckpoint(bool reuseCompleted)
    {
        using var fixture = new Fixture();
        var state = new ProcessingState
        {
            ReusesCompletedFrames = reuseCompleted,
            Dialog = new()
            {
                FrameSets =
            [new() { Finished = true, Frames = Enumerable.Range(0, 15000).Select(i => new FrameResultDto(i, 1234, 2345)).ToList() }, new() { Finished = true,
                Frames = Enumerable.Range(0, 24000).Select(i => new FrameResultDto(i, 4321, 5432)).ToList() }]
            }
        };
        IncrementalProgressStore.SaveChunked(fixture.Manifest, state);
        var root = JsonNode.Parse(File.ReadAllBytes(fixture.Manifest))!;
        var frames = root["Dialog"]!["FrameSets"]!;
        var correct = frames[0]!["$pack"]!.GetValue<string>();
        var other = frames[1]!["$pack"]!.GetValue<string>();
        Assert.NotEqual(correct, other);
        frames[0]!["$pack"] = other;
        File.WriteAllText(fixture.Manifest, root.ToJsonString());
        Assert.Throws<InvalidDataException>(() => IncrementalProgressStore.Load(fixture.Manifest));
        IncrementalProgressStore.SaveChunked(fixture.Manifest, state);
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(IncrementalProgressStore.Load(fixture.Manifest)));
        root = JsonNode.Parse(File.ReadAllBytes(fixture.Manifest))!;
        Assert.Equal(correct, root["Dialog"]!["FrameSets"]![0]!["$pack"]!.GetValue<string>());
    }

    [Fact]
    public void VersionOneChunksLoadAndRemainReusableInVersionTwo()
    {
        using var fixture = new Fixture();
        var done = new DialogFrameSetDto { Finished = true, Frames = [new(2, 10, 20)] };
        var state = new ProcessingState { Timecodes = [0, 40], Dialog = new() { FrameSets = [done] } };
        Directory.CreateDirectory(fixture.Chunks);
        var payload = JsonSerializer.SerializeToUtf8Bytes(done);
        var hash = Hash(payload);
        var chunk = Path.Combine(fixture.Chunks, hash + ".json");
        File.WriteAllBytes(chunk, payload);
        File.SetLastWriteTimeUtc(chunk, DateTime.UtcNow.AddDays(-1));
        var stamp = File.GetLastWriteTimeUtc(chunk);
        var root = JsonSerializer.SerializeToNode(state)!.AsObject();
        root["Version"] = "chunks1:" + state.Version;
        root["CheckpointStorage"] = 1;
        root["Dialog"]!["FrameSets"]![0] = new JsonObject { ["$chunk"] = hash };
        File.WriteAllText(fixture.Manifest, root.ToJsonString());
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(IncrementalProgressStore.Load(fixture.Manifest)));
        IncrementalProgressStore.SaveChunked(fixture.Manifest, state);
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(chunk));
        root = JsonNode.Parse(File.ReadAllBytes(fixture.Manifest))!.AsObject();
        Assert.Equal(2, root["CheckpointStorage"]!.GetValue<int>());
        Assert.Null(root["Dialog"]!["FrameSets"]![0]!["$pack"]);
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(IncrementalProgressStore.Load(fixture.Manifest)));
        IncrementalProgressStore.Delete(fixture.Manifest);
        Assert.False(Directory.Exists(fixture.Chunks));
    }

    [Fact]
    public void ManyEventsAndTimecodeBlocksShareOneImmutablePackAndFailedCommitKeepsPriorGeneration()
    {
        using var fixture = new Fixture();
        var completed = Enumerable.Range(0, 8).Select(i => new DialogFrameSetDto
        { Finished = true, Frames = [new(i, 10, 20)] }).ToList();
        var state = new ProcessingState
        {
            ReusesCompletedFrames = true,
            Timecodes = Enumerable.Range(0, 9000).ToList(),
            Dialog = new() { FrameSets = completed }
        };
        IncrementalProgressStore.SaveChunked(fixture.Manifest, state);
        var pack = Assert.Single(Directory.GetFiles(fixture.Chunks));
        File.SetLastWriteTimeUtc(pack, DateTime.UtcNow.AddDays(-1));
        var stamp = File.GetLastWriteTimeUtc(pack);
        IncrementalProgressStore.SaveChunked(fixture.Manifest, state);
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(pack));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(IncrementalProgressStore.Load(fixture.Manifest)));
        var prior = File.ReadAllBytes(fixture.Manifest);
        var next = new ProcessingState
        {
            FrameIndex = 1,
            ReusesCompletedFrames = true,
            Timecodes = state.Timecodes,
            Dialog = new() { FrameSets = [.. completed, new() { Finished = true, Frames = [new(9999, 11, 21)] }] }
        };
        using (var locked = new FileStream(fixture.Manifest, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = Record.Exception(() => IncrementalProgressStore.SaveChunked(fixture.Manifest, next));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal(prior, File.ReadAllBytes(fixture.Manifest));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(pack));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(IncrementalProgressStore.Load(fixture.Manifest)));
        IncrementalProgressStore.SaveChunked(fixture.Manifest, next);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(IncrementalProgressStore.Load(fixture.Manifest)));
        Assert.Equal(2, Directory.GetFiles(fixture.Chunks).Length);
        IncrementalProgressStore.Delete(fixture.Manifest);
        Assert.False(Directory.Exists(fixture.Chunks));
    }

    [Fact]
    public void MissingPackIsRebuiltFromImmutableSnapshotEvenWithCachedPayloadIdentities()
    {
        using var fixture = new Fixture();
        var state = new ProcessingState
        {
            ReusesCompletedFrames = true,
            Marker = new() { FrameSets = [new() { Finished = true, Frames = [new(2, 3, 4)] }] }
        };
        IncrementalProgressStore.SaveChunked(fixture.Manifest, state);
        var pack = Assert.Single(Directory.GetFiles(fixture.Chunks));
        File.Delete(pack);
        Assert.ThrowsAny<IOException>(() => IncrementalProgressStore.Load(fixture.Manifest));
        IncrementalProgressStore.SaveChunked(fixture.Manifest, state);
        Assert.True(File.Exists(pack));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(IncrementalProgressStore.Load(fixture.Manifest)));
    }

    [Fact]
    public void PackAndPayloadChecksumsMissingMembersAndVersionedReferencesAreValidated()
    {
        using var fixture = new Fixture();
        var state = new ProcessingState { Dialog = new() { FrameSets = [new() { Finished = true, Frames = [new(2, 3, 4)] }] } };
        IncrementalProgressStore.SaveChunked(fixture.Manifest, state);
        var root = JsonNode.Parse(File.ReadAllBytes(fixture.Manifest))!.AsObject();
        var reference = root["Dialog"]!["FrameSets"]![0]!.AsObject();
        var hash = reference["$chunk"]!.GetValue<string>();
        var pack = reference["$pack"]!.GetValue<string>();
        var packed = JsonNode.Parse(File.ReadAllBytes(Path.Combine(fixture.Chunks, pack + ".json")))!;
        packed["$chunks"]![hash]!["Frames"]![0]!["X"] = 999;
        var forged = JsonSerializer.SerializeToUtf8Bytes(packed);
        var forgedHash = Hash(forged);
        File.WriteAllBytes(Path.Combine(fixture.Chunks, forgedHash + ".json"), forged);
        reference["$pack"] = forgedHash;
        void Reject()
        {
            File.WriteAllText(fixture.Manifest, root.ToJsonString());
            Assert.Throws<InvalidDataException>(() => IncrementalProgressStore.Load(fixture.Manifest));
        }
        Reject(); // Valid outer checksum cannot hide a changed inner payload.
        reference["$chunk"] = new string('A', 64);
        Reject(); // Missing member in an otherwise readable pack.
        reference["$chunk"] = hash;
        reference["$pack"] = "../outside";
        Reject();
        reference["$pack"] = pack;
        root["CheckpointStorage"] = 1;
        root["Version"] = "chunks1:" + state.Version;
        Reject(); // Packed references are never accepted under the old format.
        root["CheckpointStorage"] = 2;
        root["Version"] = "chunks2:" + state.Version;
        reference.Remove("$chunk");
        Reject();
    }

    [Fact]
    public void PackPublicationFailureNeverReplacesManifestAndCleanupDoesNotTraverseDirectories()
    {
        using var fixture = new Fixture();
        IncrementalProgressStore.SaveChunked(fixture.Manifest, new ProcessingState { FrameIndex = 10 });
        var prior = File.ReadAllBytes(fixture.Manifest);
        var done = new DialogFrameSetDto { Finished = true, Frames = [new(2, 3, 4)] };
        var payload = JsonSerializer.SerializeToUtf8Bytes(done);
        var packed = Encoding.UTF8.GetBytes("{\"$chunks\":{\"" + Hash(payload) + "\":" + Encoding.UTF8.GetString(payload) + "}}");
        var collision = Path.Combine(fixture.Chunks, Hash(packed) + ".json");
        Directory.CreateDirectory(collision);
        var error = Record.Exception(() => IncrementalProgressStore.SaveChunked(fixture.Manifest,
            new ProcessingState { FrameIndex = 20, Dialog = new() { FrameSets = [done] } }));
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Equal(prior, File.ReadAllBytes(fixture.Manifest));
        Assert.Equal(10, IncrementalProgressStore.Load(fixture.Manifest).FrameIndex);
        Assert.Empty(Directory.GetFiles(fixture.Chunks, "*.tmp"));
        IncrementalProgressStore.Delete(fixture.Manifest);
        Assert.True(Directory.Exists(collision));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "SekaiTools-packs-" + Guid.NewGuid().ToString("N"));
        public string Manifest => Path.Combine(_root, "task.json");
        public string Chunks => Manifest + ".chunks";
        public Fixture() => Directory.CreateDirectory(_root);
        public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    }
}
