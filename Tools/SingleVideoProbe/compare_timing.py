"""Compare timing candidates to an existing script-assisted result, without text matching."""
import argparse
import json
from pathlib import Path
import statistics

from probe import write_json


def overlap(a, b):
    intersection = max(0, min(a["end"], b["end"]) - max(a["start"], b["start"]))
    union = max(a["end"], b["end"]) - min(a["start"], b["start"])
    return intersection / union if union > 0 else 0


def compare(report, state):
    if Path(report["video"]).resolve() != Path(state["VideoFilePath"]).resolve():
        raise ValueError("Reference state belongs to another video")
    reference = [{"start": fs["Frames"][0]["Index"] / report["fps"],
                  "end": (fs["Frames"][-1]["Index"] + 1) / report["fps"]}
                 for fs in state["Dialog"]["FrameSets"] if fs["Frames"]]
    observed = report["dialogs"]
    n, m = len(reference), len(observed)
    costs = [[0.] * (m + 1) for _ in range(n + 1)]
    moves = {}
    for i in range(1, n + 1):
        costs[i][0], moves[i, 0] = float(i), "missing"
    for j in range(1, m + 1):
        costs[0][j], moves[0, j] = float(j), "extra"
    for i in range(1, n + 1):
        for j in range(1, m + 1):
            iou = overlap(reference[i - 1], observed[j - 1])
            costs[i][j], moves[i, j] = min(
                (costs[i - 1][j - 1] + (1 - iou if iou >= .1 else 3), "pair"),
                (costs[i - 1][j] + 1, "missing"), (costs[i][j - 1] + 1, "extra"))
    i, j = n, m
    pairs = []
    while i or j:
        move = moves[i, j]
        pair = {"kind": move, "reference_index": i - 1 if move != "extra" else None,
                "observed_index": j - 1 if move != "missing" else None}
        if move == "pair":
            a, b = reference[i - 1], observed[j - 1]
            pair.update({"iou": overlap(a, b), "start_delta": b["start"] - a["start"],
                         "end_delta": b["end"] - a["end"]})
        pairs.append(pair)
        i -= move != "extra"
        j -= move != "missing"
    pairs.reverse()
    matched = [p for p in pairs if p["kind"] == "pair"]
    result = {"reference_dialogs": n, "candidates": m, "matched": len(matched),
              "missing": sum(p["kind"] == "missing" for p in pairs),
              "extra": sum(p["kind"] == "extra" for p in pairs),
              "pairing": "monotonic maximum interval-overlap alignment; IoU >= 0.1",
              "reference": "script-assisted timings, not manually annotated ground truth", "pairs": pairs}
    if matched:
        for boundary in ("start", "end"):
            values = [abs(p[boundary + "_delta"]) for p in matched]
            result[boundary + "_mae_seconds"] = statistics.mean(values)
            result[boundary + "_max_seconds"] = max(values)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("recognition")
    parser.add_argument("reference_state")
    parser.add_argument("output")
    args = parser.parse_args()
    result = compare(json.loads(Path(args.recognition).read_text(encoding="utf-8")),
                     json.loads(Path(args.reference_state).read_text(encoding="utf-8")))
    write_json(args.output, result)
    print(json.dumps({k: v for k, v in result.items() if k != "pairs"}))
