"""Verify complete decoder experiment outputs; no input files are modified."""
import hashlib
import json
import sys
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def verify(reference, candidate):
    if not __debug__:
        raise RuntimeError("Verification requires assertions; do not use Python -O")
    a = json.loads((reference / "run-1.json").read_text(encoding="utf-8"))
    b = json.loads((candidate / "run-1.json").read_text(encoding="utf-8"))
    x = json.loads((reference / "state-1.json").read_text(encoding="utf-8"))
    y = json.loads((candidate / "state-1.json").read_text(encoding="utf-8"))
    for directory, report in [(reference, a), (candidate, b)]:
        assert digest(directory / "state-1.json") == report["StateHash"], "State file hash mismatch"
        assert digest(directory / "subtitle-1.ass") == report["SubtitleHash"], "ASS file hash mismatch"
        assert not report["HardwareDecode"]
        assert report["DecodeBackend"] == "FFMPEG" and report["DecodeAcceleration"] == "None"
    for key in ["Video", "Run", "Warmup", "Preview", "Mode", "PrepareGray", "OpenCvThreads",
                "DetailedProfile", "Resolution", "VideoFrames", "FrameBufferCapacity", "ContentPositionHints", "StopReason"]:
        assert a[key] == b[key], f"Configuration differs: {key}"
    assert a["StopReason"] in ("Completed", "EndOfStream"), "Unexpected termination"
    assert a["Performance"]["FrameCount"] == b["Performance"]["FrameCount"], "Measured frame counts differ"
    assert a["RequestedDecoderThreads"] is None
    assert b["RequestedDecoderThreads"] == b["ActualDecoderThreads"]
    assert a["OriginalConfigHash"] == b["OriginalConfigHash"] == x["Metadata"]["ConfigHash"]
    tag = f'{b["OriginalConfigHash"]}|BenchmarkOnlyDecoder|{b["DecodeBackend"]}:{b["DecodeAcceleration"]}:{b["RequestedDecoderThreads"]}'
    assert hashlib.sha256(tag.encode()).hexdigest().upper() == y["Metadata"]["ConfigHash"], "Unexpected experiment identity"
    y["Metadata"]["ConfigHash"] = b["OriginalConfigHash"]
    assert x == y, "Complete state differs beyond validated experiment identity"
    assert a["StateHashBeforeExperimentTag"] == b["StateHashBeforeExperimentTag"] == a["StateHash"]
    assert (reference / "subtitle-1.ass").read_bytes() == (candidate / "subtitle-1.ass").read_bytes()
    return {"Video": a["Video"], "ReferenceDecoderThreads": a["ActualDecoderThreads"],
            "CandidateDecoderThreads": b["ActualDecoderThreads"], "StopReason": a["StopReason"],
            "CompleteStateEquivalent": True, "AssBytesEqual": True,
            "ReferenceStateHash": a["StateHash"], "CandidateStateHash": b["StateHash"]}


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("Usage: VerifyDecoderPair.py <reference-output> <candidate-output>")
    print(json.dumps(verify(Path(sys.argv[1]), Path(sys.argv[2])), ensure_ascii=True))
