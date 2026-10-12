import hashlib
import json
import pathlib
import sys

reference, canceled, resumed = map(pathlib.Path, sys.argv[1:4])
read = lambda p: json.loads(p.read_text(encoding="utf-8-sig"))
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest().upper()
baseline = read(reference / "run-1.json")
reports = [read(p / "report.json") for p in (canceled, resumed)]
states = [read(p / "state.json") for p in (canceled, resumed)]
expected = read(reference / "state-1.json")
identity = baseline["CaptureIdentity"]
assert baseline["ProcessorFactoryCapture"] and identity
assert sha(reference / "state-1.json") == baseline["StateHash"]
assert sha(reference / "subtitle-1.ass") == baseline["SubtitleHash"]
for directory, report, state in zip((canceled, resumed), reports, states):
    assert report["ProcessorFactoryCapture"] and report["SessionDisposed"] and report["CaptureDisposed"]
    assert report["ProductFactoryImplementation"], "Actual infrastructure factory required"
    assert report["ProductRejectsExperimentState"]
    assert report["CaptureIdentity"] == state["Metadata"]["CaptureIdentity"] == identity
    assert report["OriginalConfigHash"] == report["ExperimentConfigHash"] == state["Metadata"]["ConfigHash"]
    assert report["PreviewFrames"] > 0 and report["NonColorPreviewFrames"] == 0
    assert report["NativeHash"] == baseline["PeriodicColorNativeHash"]
    assert report["PluginHash"] == baseline["PeriodicColorPluginHash"]
    assert sha(pathlib.Path(report["NativePath"])) == report["NativeHash"]
    assert sha(pathlib.Path(report["PluginPath"])) == report["PluginHash"]
    assert sha(pathlib.Path(report["Video"])) == report["InputHash"]
    assert sha(directory / "state.json") == report["StateHash"]
    assert report["MatchingThreads"] == 16 and report["DecoderThreads"] == 16 and report["Buffer"] is None
c, r = reports
assert c["StopReason"] == "Canceled" and c["CheckpointEquivalent"]
assert c["FrameIndex"] == c["CancelFrame"] == r["ResumeFrame"]
checkpoint = pathlib.Path(c["CheckpointPath"])
assert sha(checkpoint) == c["CheckpointHash"] == r["SourceCheckpointHash"]
assert read(checkpoint) == states[0]
assert r["HistoryWritten"] and r["HistoryEquivalent"]
history = pathlib.Path(r["HistoryPath"])
assert sha(history) == r["HistoryHash"] and read(history) == states[1]
assert states[1] == expected, "Complete restored state differs; no normalization allowed"
assert sha(resumed / "subtitle.ass") == r["SubtitleHash"]
assert (resumed / "subtitle.ass").read_bytes() == (reference / "subtitle-1.ass").read_bytes()
print(json.dumps({"VerificationPassed": True, "Frame": c["CancelFrame"],
    "FullStateAndSubtitleEqual": True, "IdentityNormalized": False,
    "AtomicCheckpointAndHistoryEqual": True, "ProcessorReleasedSession": True,
    "NoPerformanceConclusion": True}))
