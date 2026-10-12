"""Audit five groups / three interleaved rounds against the original package."""
import json
import math
import statistics
import sys
from pathlib import Path
from VerifyGrayPair import digest, verify


def summarize(root):
    if not __debug__:
        raise RuntimeError("Do not use Python -O")
    manifest = json.loads((root / "manifest-before.json").read_text(encoding="utf-8-sig"))
    identities = {}
    for entry in manifest:
        path = Path(entry["Path"])
        assert digest(path) == entry["Hash"], f"Manifest changed: {path}"
        identities[str(path.resolve()).casefold()] = entry["Hash"]
    schedule = json.loads((root / "schedule.json").read_text(encoding="utf-8-sig"))
    assert schedule["Rounds"] == 3 and len(set(schedule["Videos"])) == 5
    assert schedule["ExpectedReferenceNativeHash"] == "0A697D9F6CE35463ECC434CD881920EBDEB2E075F305BEE04685A44278B98D12"
    assert schedule["ReferenceIdentity"] == "OriginalPackage" and schedule["CandidateIdentity"] == "IppNativeGray"
    assert len(list(root.glob("*/round-*/*/run-1.json"))) == 30
    rows = []
    for video in schedule["Videos"]:
        pairs = []
        reference_states, reference_ass = set(), set()
        group = root / Path(video).stem
        assert {p.name for p in group.glob("round-*")} == {"round-1", "round-2", "round-3"}
        for round_number in range(1, 4):
            pair = group / f"round-{round_number}"
            a_path, b_path = pair / "reference", pair / "gray"
            verify(a_path, b_path, require_native_identity=True)
            a, b = [json.loads((p / "run-1.json").read_text(encoding="utf-8")) for p in (a_path, b_path)]
            assert a["Video"] == b["Video"] == video
            assert a["OpenCvThreads"] == 4 and a["FrameBufferCapacity"] == 8
            assert a["Mode"] == "Automatic" and not a["DetailedProfile"] and not a["ContentPositionHints"]
            for report, expected in ((a, schedule["ExpectedReferenceNativeHash"]), (b, schedule["ExpectedCandidateNativeHash"])):
                core = next(item for item in report["NativeLibraries"] if Path(item["Path"]).name.lower() == "cvextern.dll")
                assert core["Hash"] == expected, "Wrong actual runtime"
                for item in report["NativeLibraries"]:
                    assert identities[str(Path(item["Path"]).resolve()).casefold()] == item["Hash"], "Runtime outside qualified manifest"
            reference_states.add(a["StateHash"])
            reference_ass.add(a["SubtitleHash"])
            reference_seconds, gray_seconds = a["WallMilliseconds"] / 1000, b["WallMilliseconds"] / 1000
            assert math.isfinite(reference_seconds) and math.isfinite(gray_seconds)
            assert reference_seconds > 0 and gray_seconds > 0
            pairs.append({"Round": round_number, "ReferenceSeconds": reference_seconds, "GraySeconds": gray_seconds,
                          "ReductionPercent": (1 - gray_seconds / reference_seconds) * 100,
                          "ReferencePeakMiB": a["PeakWorkingSet"] / 1048576, "GrayPeakMiB": b["PeakWorkingSet"] / 1048576})
        assert len(reference_states) == len(reference_ass) == 1, "Cross-round outputs changed"
        reductions = [pair["ReductionPercent"] for pair in pairs]
        reference_median = statistics.median(pair["ReferenceSeconds"] for pair in pairs)
        gray_median = statistics.median(pair["GraySeconds"] for pair in pairs)
        rows.append({"Video": video, "CompleteStateAndAssEquivalent": True, "Pairs": pairs,
                     "PairedReductionMedianPercent": statistics.median(reductions),
                     "ReductionRangePercent": [min(reductions), max(reductions)],
                     "ReferenceMedianSeconds": reference_median, "GrayMedianSeconds": gray_median,
                     "RatioOfMediansReductionPercent": (1 - gray_median / reference_median) * 100,
                     "GuiPreviewQualified": False, "ResumeQualified": False})
    (root / "summary.json").write_text(json.dumps(rows, indent=2, ensure_ascii=True), encoding="utf-8")
    return rows


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("SummarizeGrayComparison.py <comparison-output-root>")
    print(json.dumps(summarize(Path(sys.argv[1])), ensure_ascii=True))
