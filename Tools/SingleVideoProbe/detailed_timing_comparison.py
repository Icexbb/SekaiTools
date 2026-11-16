"""Export per-dialogue timing differences and split/merge evidence after recognition."""
import argparse
import csv
import hashlib
import json
import math
from pathlib import Path
import statistics

from compare_timing import compare


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("recognition")
    parser.add_argument("reference_state")
    parser.add_argument("script")
    parser.add_argument("output", help="New output directory")
    parser.add_argument("--observations", help="Optional C# --diagnostics observations.jsonl")
    args = parser.parse_args()
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=False)
    observed = json.loads(Path(args.recognition).read_text(encoding="utf-8"))
    state = json.loads(Path(args.reference_state).read_text(encoding="utf-8"))
    script = json.loads(Path(args.script).read_text(encoding="utf-8-sig"))
    if Path(state["ScriptFilePath"]).resolve() != Path(args.script).resolve():
        raise ValueError("Reference state belongs to another script")
    reference = state["Dialog"]["FrameSets"]
    talks = script["TalkData"]
    if len(reference) != len(talks):
        raise ValueError("Reference recognition is incomplete; dialogue indices cannot be assumed")
    if any(not item["Finished"] or not item["Frames"] for item in reference):
        raise ValueError("Reference contains unfinished or empty dialogue intervals")
    observations = ([json.loads(line) for line in Path(args.observations).read_text(encoding="utf-8-sig").splitlines()]
                    if args.observations else [])
    result = compare(observed, state)
    fps = observed["fps"]
    candidates = [(round(item["start"] * fps), round(item["end"] * fps)) for item in observed["dialogs"]]
    rows = []
    for match in result["pairs"]:
        i, j = match["reference_index"], match["observed_index"]
        row = {"kind": match["kind"], "script_dialogue": i + 1 if i is not None else "",
               "video_candidate": j + 1 if j is not None else ""}
        if i is not None:
            start, end = reference[i]["Frames"][0]["Index"], reference[i]["Frames"][-1]["Index"] + 1
            intersections = [(k + 1, max(0, min(end, b) - max(start, a)))
                             for k, (a, b) in enumerate(candidates)]
            intersections = [(k, length) for k, length in intersections if length > 0]
            # Candidate segments do not overlap; union their intersections defensively.
            clipped = sorted((max(start, a), min(end, b)) for a, b in candidates if a < end and b > start)
            covered = 0
            previous_end = start
            for a, b in clipped:
                covered += max(0, b - max(a, previous_end))
                previous_end = max(previous_end, b)
            points = {(frame["X"], frame["Y"]) for frame in reference[i]["Frames"]}
            row.update({"speaker": talks[i]["WindowDisplayName"], "body": talks[i]["Body"],
                        "reference_start_frame": start, "reference_end_exclusive_frame": end,
                        "reference_start_seconds": start / fps, "reference_end_seconds": end / fps,
                        "overlapping_candidates": ",".join(str(k) for k, _ in intersections),
                        "coverage_fraction": covered / (end - start), "reference_position_count": len(points),
                        "reference_x_span": max(x for x, _ in points) - min(x for x, _ in points),
                        "reference_y_span": max(y for _, y in points) - min(y for _, y in points)})
            if observations:
                samples = [item for item in observations if start <= item["Frame"] < end]
                row.update({"diagnostic_samples": len(samples),
                            "diagnostic_visible": sum(item["Visible"] for item in samples),
                            "diagnostic_menu_failures": sum(not item["MenuPresent"] for item in samples),
                            "diagnostic_line_failures": sum(not item["NameLinePresent"] for item in samples),
                            "diagnostic_glyph_failures": sum(item["NamePixels"] < 30 or item["BodyPixels"] < 35 for item in samples),
                            "diagnostic_reset_evidence": sum(item["Visible"] and item["RetainedFraction"] < .65 for item in samples)})
        if j is not None:
            a, b = candidates[j]
            row.update({"candidate_start_frame": a, "candidate_end_exclusive_frame": b,
                        "candidate_start_seconds": a / fps, "candidate_end_seconds": b / fps})
        if i is not None and j is not None:
            row.update({"start_delta_frames": a - start, "end_delta_frames": b - end,
                        "start_delta_seconds": (a - start) / fps, "end_delta_seconds": (b - end) / fps,
                        "iou": match["iou"]})
        rows.append(row)
    fields = list(dict.fromkeys(key for row in rows for key in row))
    with (output / "dialogue-differences.csv").open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)
    matched = [row for row in rows if row["kind"] == "pair"]
    for boundary in ("start", "end"):
        values = sorted(abs(row[boundary + "_delta_seconds"]) for row in matched)
        result[boundary + "_median_seconds"] = statistics.median(values) if values else None
        result[boundary + "_p95_seconds"] = values[math.ceil(len(values) * .95) - 1] if values else None
    result["pairs_with_both_boundaries_within_5_frames"] = sum(
        abs(row["start_delta_frames"]) <= 5 and abs(row["end_delta_frames"]) <= 5 for row in matched)
    result["rows"] = rows
    result["fps"] = fps
    result["frame_interval_seconds"] = 1 / fps
    result["script_banners"] = sum(effect.get("EffectType") == 8 for effect in script["SpecialEffectData"])
    result["input_sha256"] = {}
    for name in ("recognition", "reference_state", "script"):
        with Path(getattr(args, name)).open("rb") as stream:
            result["input_sha256"][name] = hashlib.file_digest(stream, "sha256").hexdigest()
    (output / "detailed-comparison.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({key: value for key, value in result.items() if key not in ("rows", "pairs")}))


if __name__ == "__main__":
    main()
