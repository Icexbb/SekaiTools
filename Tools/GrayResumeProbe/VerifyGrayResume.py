"""Compare an atomic canceled checkpoint / resumed run with full recognition."""
import hashlib
import json
import sys
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def verify(reference, canceled, resumed):
    if not __debug__:
        raise RuntimeError("Do not disable assertions with Python -O")
    a = json.loads((reference / "run-1.json").read_text(encoding="utf-8"))
    x = json.loads((reference / "state-1.json").read_text(encoding="utf-8"))
    b, c = [json.loads((directory / "report.json").read_text(encoding="utf-8")) for directory in (canceled, resumed)]
    y, z = [json.loads((directory / "state.json").read_text(encoding="utf-8")) for directory in (canceled, resumed)]
    assert digest(reference / "state-1.json") == a["StateHash"]
    assert digest(reference / "subtitle-1.ass") == a["SubtitleHash"]
    assert a["NativeGrayCapture"] and not a["Preview"] and not a["PrepareGray"]
    assert not a["HardwareDecode"] and a["ActualDecoderThreads"] == 16
    assert a["DecodeBackend"] == "FFMPEG" and a["DecodeAcceleration"] == "None"
    for directory, report, state in ((canceled, b, y), (resumed, c, z)):
        assert digest(directory / "state.json") == report["StateHash"]
        assert report["Video"] == a["Video"] and digest(Path(report["Video"])) == report["InputHash"]
        assert report["NativeHash"] == a["GrayCaptureNativeHash"] == digest(Path(report["NativePath"]))
        assert report["PluginHash"] == a["GrayCapturePluginHash"] == digest(Path(report["PluginPath"]))
        assert report["MatchingThreads"] == a["OpenCvThreads"]
        assert report["Buffer"] == a["FrameBufferCapacity"] and report["DecoderThreads"] == 16
        assert report["OriginalConfigHash"] == a["OriginalConfigHash"]
        assert report["ProductRejectsExperimentState"] and report["NoPerformanceConclusion"] and not report["PreviewQualified"]
        assert report["CaptureDisposed"]
        assert report["SaveCount"] > 0
        tag = (f'{report["OriginalConfigHash"]}|BenchmarkOnlyGrayCapture|FFMPEG:None:16:'
               f'{report["NativeHash"]}:{report["PluginHash"]}')
        identity = hashlib.sha256(tag.encode()).hexdigest().upper()
        assert report["ExperimentConfigHash"] == state["Metadata"]["ConfigHash"] == identity
    assert b["Mode"] == "cancel" and b["StopReason"] == "Canceled"
    assert b["CancelFrame"] == b["FrameIndex"] == y["FrameIndex"] > 0
    assert b["CheckpointEquivalent"]
    checkpoint = Path(b["CheckpointPath"])
    assert checkpoint.resolve().is_relative_to(canceled.resolve()), "Checkpoint outside canceled output"
    assert digest(checkpoint) == b["CheckpointHash"]
    assert json.loads(checkpoint.read_text(encoding="utf-8")) == y, "Saved checkpoint differs from captured state"
    assert c["Mode"] == "resume" and c["CancelFrame"] is None
    assert c["SourceCheckpointHash"] == b["CheckpointHash"]
    assert c["ResumeFrame"] == b["FrameIndex"]
    assert c["StopReason"] == a["StopReason"] and c["StopReason"] in ("Completed", "EndOfStream")
    if c["StopReason"] == "Completed":
        assert c["HistoryWritten"] and c["HistoryEquivalent"]
        history = Path(c["HistoryPath"])
        assert history.resolve().is_relative_to(resumed.resolve())
        assert digest(history) == c["HistoryHash"]
        assert json.loads(history.read_text(encoding="utf-8")) == z, "Atomic history differs from final state"
    assert x == z, "Complete restored state differs from continuous processing"
    assert c["StateHash"] == a["StateHash"]
    assert digest(resumed / "subtitle.ass") == c["SubtitleHash"] == a["SubtitleHash"]
    assert (reference / "subtitle-1.ass").read_bytes() == (resumed / "subtitle.ass").read_bytes()
    return {"Video": a["Video"], "CheckpointFrame": b["FrameIndex"], "AtomicCheckpointEquivalent": True,
            "FullRestoredStateEquivalent": True, "AssBytesEqual": True,
            "ProductRejectsExperimentState": True, "NoPerformanceConclusion": True}


if __name__ == "__main__":
    if len(sys.argv) != 4:
        raise SystemExit("VerifyGrayResume.py <continuous-recognition-directory> <canceled-output> <resumed-output>")
    print(json.dumps(verify(*(Path(arg) for arg in sys.argv[1:])), ensure_ascii=True))
