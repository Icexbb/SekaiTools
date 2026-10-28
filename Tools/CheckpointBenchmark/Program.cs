using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using SekaiToolsCore.Process;
using SekaiToolsInfrastructure.Persistence;

if (args.Length != 2) throw new ArgumentException("CheckpointBenchmark <new-output-directory> <round=1..4>");
var output = Path.GetFullPath(args[0]);
var round = int.Parse(args[1]);
if (round is < 1 or > 4 || Directory.Exists(output)) throw new ArgumentException("Use a new output directory and round 1..4");
Directory.CreateDirectory(output);
var store = typeof(SubtitleQueueProgressStore).Assembly.GetType("SekaiToolsInfrastructure.Persistence.IncrementalProgressStore", true)!;
var legacy = typeof(ProgressStore).GetMethod("SaveToPath", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Action<string, ProcessingState>>();
var adaptive = store.GetMethod("Save")!.CreateDelegate<Action<string, ProcessingState>>();
var chunked = store.GetMethod("SaveChunked")!.CreateDelegate<Action<string, ProcessingState>>();
var load = store.GetMethod("Load")!.CreateDelegate<Func<string, ProcessingState>>();
var delete = store.GetMethod("Delete")!.CreateDelegate<Action<string>>();
var atomic = store.GetMethod("WriteAtomic", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Action<string, byte[]>>();
Action<string, ProcessingState> compact = (path, state) => atomic(path, JsonSerializer.SerializeToUtf8Bytes(state));
var modes = new (string Name, Action<string, ProcessingState> Save)[]
    { ("legacy", legacy), ("compact", compact), ("adaptive", adaptive), ("chunked", chunked) };
var results = new List<object>();
foreach (var eventCount in new[] { 4, 64 })
    for (var order = 0; order < modes.Length; order++)
    {
        var mode = modes[(order + round - 1) % modes.Length];
        var path = Path.Combine(output, $"{eventCount}-{mode.Name}.json");
        // Fresh DTO identities per mode; immutable blocks are reused only within
        // the five snapshots, as they are in one real processor lifetime.
        var completed = Enumerable.Range(0, eventCount).Select(e => new DialogFrameSetDto
        {
            Finished = true,
            Frames = Enumerable.Range(e * 4000, 4000).Select(i => new FrameResultDto(i, 1234, 2345)).ToList()
        }).ToList();
        var times = Enumerable.Range(0, eventCount == 4 ? 20000 : 300000).Select(i => i * 17 + i / 3).ToList();
        var initialTimeCount = times.Count;
        var frozenPrefix = times.Take(times.Count / 4096 * 4096).Chunk(4096).ToArray();
        var saves = new List<double>();
        var sizes = new List<long>();
        var chunkFiles = new List<int>();
        for (var step = 0; step < 5; step++)
        {
            times.AddRange(Enumerable.Range(times.Count, 64).Select(i => i * 17 + i / 3));
            int[][] blocks = [.. frozenPrefix, times.Skip(frozenPrefix.Length * 4096).ToArray()];
            var dialog = new DialogMatcherStateDto
            {
                FrameSets = [.. completed, new DialogFrameSetDto
                { Frames = Enumerable.Range(initialTimeCount, 64 * (step + 1)).Select(i => new FrameResultDto(i, 1234, 2345)).ToList() }]
            };
            var expected = new ProcessingState { FrameIndex = times.Count - 1, Timecodes = times, Dialog = dialog };
            var state = mode.Name is "legacy" or "compact" ? expected : new ProcessingState
            {
                FrameIndex = expected.FrameIndex,
                Dialog = dialog,
                ReusesCompletedFrames = true,
                CheckpointTimecodeBlocks = blocks
            };
            var started = Stopwatch.GetTimestamp();
            mode.Save(path, state);
            saves.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            var chunks = path + ".chunks";
            var files = Directory.Exists(chunks) ? Directory.GetFiles(chunks) : [];
            chunkFiles.Add(files.Length);
            sizes.Add(new FileInfo(path).Length + files.Sum(f => new FileInfo(f).Length));
            if (JsonSerializer.Serialize(load(path)) != JsonSerializer.Serialize(expected))
                throw new InvalidDataException($"State mismatch: {mode.Name}/{eventCount}/{step}");
        }
        results.Add(new
        {
            Round = round,
            Events = eventCount,
            Mode = mode.Name,
            SaveMilliseconds = saves,
            TotalMilliseconds = saves.Sum(),
            BytesOnDisk = sizes,
            ChunkFiles = chunkFiles,
            AllRestoredStatesEqual = true
        });
        delete(path);
    }
File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(results));
