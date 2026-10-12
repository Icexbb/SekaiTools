"""Audit complete native-gray recognition outputs without altering evidence."""
import hashlib
import json
import sys
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def verify(reference, candidate, require_native_identity=False):
    if not __debug__:
        raise RuntimeError("Do not disable verification with Python -O")
    a, b = [json.loads((directory / "run-1.json").read_text(encoding="utf-8"))
            for directory in (reference, candidate)]
    x, y = [json.loads((directory / "state-1.json").read_text(encoding="utf-8"))
            for directory in (reference, candidate)]
    for directory, report in ((reference, a), (candidate, b)):
        assert digest(directory / "state-1.json") == report["StateHash"], "State hash mismatch"
        assert digest(directory / "subtitle-1.ass") == report["SubtitleHash"], "ASS hash mismatch"
        assert not report["HardwareDecode"] and not report["Preview"] and not report["PrepareGray"]
        assert report["DecodeBackend"] == "FFMPEG" and report["DecodeAcceleration"] == "None"
        assert report["ActualDecoderThreads"] == 16
    assert not a["NativeGrayCapture"] and b["NativeGrayCapture"]
    assert a["RequestedDecoderThreads"] is None and b["RequestedDecoderThreads"] == 16
    for key in ("Video", "Run", "Warmup", "Mode", "OpenCvThreads", "DetailedProfile", "Resolution",
                "VideoFrames", "FrameBufferCapacity", "ContentPositionHints", "StopReason"):
        assert a[key] == b[key], f"Configuration differs: {key}"
    assert not a["Warmup"] and a["Run"] == 1
    assert a["StopReason"] in ("Completed", "EndOfStream")
    assert a["Performance"]["FrameCount"] == b["Performance"]["FrameCount"]
    assert a["OriginalConfigHash"] == b["OriginalConfigHash"] == x["Metadata"]["ConfigHash"]
    assert b["GrayCapturePluginHash"] == "7AABFF029D1FA47CF56EDE4DBBE05DC62DA3208F07CD16471D4C966A33F9AA31"
    assert len(b["GrayCaptureNativeHash"]) == 64
    identities_recorded = "NativeLibraries" in a and "NativeLibraries" in b
    if "NativeLibraries" in a or "NativeLibraries" in b:
        assert identities_recorded, "Runtime identity missing from one report"
    if require_native_identity:
        assert identities_recorded, "Actual loaded runtime identities required"
    if identities_recorded:
        for report in (a, b):
            libraries = {Path(entry["Path"]).name.lower(): entry for entry in report["NativeLibraries"]}
            assert set(libraries) == {"cvextern.dll", "opencv_videoio_ffmpeg500_64.dll"}
            for entry in libraries.values():
                assert digest(Path(entry["Path"])) == entry["Hash"], "Loaded runtime file changed"
            assert libraries["opencv_videoio_ffmpeg500_64.dll"]["Hash"] == b["GrayCapturePluginHash"]
            if report is b:
                assert libraries["cvextern.dll"]["Hash"] == b["GrayCaptureNativeHash"]
    tag = (f'{b["OriginalConfigHash"]}|BenchmarkOnlyGrayCapture|'
           f'{b["DecodeBackend"]}:{b["DecodeAcceleration"]}:{b["RequestedDecoderThreads"]}:'
           f'{b["GrayCaptureNativeHash"]}:{b["GrayCapturePluginHash"]}')
    assert hashlib.sha256(tag.encode()).hexdigest().upper() == y["Metadata"]["ConfigHash"], "Wrong gray experiment identity"
    y["Metadata"]["ConfigHash"] = b["OriginalConfigHash"]
    assert x == y, "Full processing state differs beyond validated experiment identity"
    assert a["StateHashBeforeExperimentTag"] == b["StateHashBeforeExperimentTag"] == a["StateHash"]
    assert (reference / "subtitle-1.ass").read_bytes() == (candidate / "subtitle-1.ass").read_bytes()
    return {"Video": a["Video"], "CompleteStateEquivalent": True, "AssBytesEqual": True,
            "ReferenceStateHash": a["StateHash"], "CandidateStateHash": b["StateHash"],
            "ReferenceSeconds": a["WallMilliseconds"] / 1000,
            "CandidateSeconds": b["WallMilliseconds"] / 1000,
            "ActualNativeIdentitiesRecorded": identities_recorded,
            "RepeatedPerformanceQualified": False}


if __name__ == "__main__":
    if len(sys.argv) not in (3, 4) or (len(sys.argv) == 4 and sys.argv[3] != "--require-native-identity"):
        raise SystemExit("VerifyGrayPair.py <ordinary-reference> <native-gray-candidate> [--require-native-identity]")
    print(json.dumps(verify(Path(sys.argv[1]), Path(sys.argv[2]), len(sys.argv) == 4), ensure_ascii=True))
