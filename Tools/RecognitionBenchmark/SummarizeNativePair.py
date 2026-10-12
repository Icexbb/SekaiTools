"""Audit a completed three-round stock/IPP decoder experiment and summarize paired times."""
import hashlib
import json
import statistics
import sys
from pathlib import Path

from VerifyDecoderPair import verify


def seconds(value):
    hours, minutes, remainder = value.split(":")
    return int(hours) * 3600 + int(minutes) * 60 + float(remainder)


def summarize(root):
    if not __debug__:
        raise RuntimeError("Verification requires assertions; do not use Python -O")
    manifest = json.loads((root / "manifest-before.json").read_text(encoding="utf-8-sig"))
    assert manifest, "Missing experiment fingerprints"
    for entry in manifest:
        with Path(entry["Path"]).open("rb") as stream:
            actual_hash = hashlib.file_digest(stream, "sha256").hexdigest().upper()
        assert actual_hash == entry["Hash"], f'Changed input or binary: {entry["Path"]}'
    groups = sorted(path for path in root.iterdir() if path.is_dir())
    assert len(groups) == 5, "Expected five input groups"
    assert len(list(root.glob("*/round-*/*/run-1.json"))) == 30, "Expected exactly 30 reports"
    rows = []
    seen_videos = set()
    for group in groups:
        assert {p.name for p in group.iterdir() if p.is_dir()} == {"round-1", "round-2", "round-3"}
        pairs = []
        stable_state = stable_ass = None
        video_identity = None
        for round_number in range(1, 4):
            directory = group / f"round-{round_number}"
            reference, candidate = directory / "stock", directory / "ipp16"
            verify(reference, candidate)
            a = json.loads((reference / "run-1.json").read_text())
            b = json.loads((candidate / "run-1.json").read_text())
            expected = {"Run": 1, "Warmup": False, "Preview": False, "Mode": "Automatic",
                        "PrepareGray": False, "OpenCvThreads": 4, "DetailedProfile": False,
                        "FrameBufferCapacity": 8, "ContentPositionHints": False}
            assert all(a[key] == value for key, value in expected.items()), "Unexpected measurement configuration"
            if video_identity is None:
                video_identity = a["Video"]
            assert a["Video"] == video_identity, "Video differs between rounds"
            assert a["ActualDecoderThreads"] == b["ActualDecoderThreads"] == 16
            assert b["RequestedDecoderThreads"] == 16
            if stable_state is None:
                stable_state, stable_ass = a["StateHash"], a["SubtitleHash"]
            assert (a["StateHash"], a["SubtitleHash"]) == (stable_state, stable_ass), "Outputs differ between rounds"
            before, after = seconds(a["Performance"]["ElapsedTime"]), seconds(b["Performance"]["ElapsedTime"])
            assert before > 0 and after > 0
            pairs.append({"Round": round_number, "StockSeconds": before, "IppSeconds": after,
                          "ReductionPercent": 100 * (before - after) / before,
                          "StockPeakMiB": a["PeakWorkingSet"] / 1048576,
                          "IppPeakMiB": b["PeakWorkingSet"] / 1048576})
        assert video_identity not in seen_videos, "Duplicate video group"
        seen_videos.add(video_identity)
        rows.append({"Video": a["Video"], "StopReason": a["StopReason"], "Pairs": pairs,
                     "PairedMedianReductionPercent": statistics.median(p["ReductionPercent"] for p in pairs),
                     "StockMedianSeconds": statistics.median(p["StockSeconds"] for p in pairs),
                     "IppMedianSeconds": statistics.median(p["IppSeconds"] for p in pairs),
                     "StockRangeSeconds": [min(p["StockSeconds"] for p in pairs), max(p["StockSeconds"] for p in pairs)],
                     "IppRangeSeconds": [min(p["IppSeconds"] for p in pairs), max(p["IppSeconds"] for p in pairs)],
                     "StockMedianPeakMiB": statistics.median(p["StockPeakMiB"] for p in pairs),
                     "IppMedianPeakMiB": statistics.median(p["IppPeakMiB"] for p in pairs),
                     "CompleteStateEquivalent": True, "AssBytesEqual": True})
    return rows


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: SummarizeNativePair.py <repeat-output-directory>")
    print(json.dumps(summarize(Path(sys.argv[1])), indent=2, ensure_ascii=True))
