"""Experimental video-only OCR. Evaluation is a separate, read-only command."""
import argparse
from collections import Counter
from difflib import SequenceMatcher
import hashlib
import html
import importlib.metadata
import json
import math
from pathlib import Path
import subprocess
import time
import unicodedata


def normalized(text):
    return "".join(unicodedata.normalize("NFKC", text).split())


def clean_speaker(text):
    # The decorative name underline is sometimes recognized as "-".
    return text if any(c.isalnum() for c in text) else ""


def distance(a, b):
    previous = list(range(len(b) + 1))
    for i, left in enumerate(a, 1):
        current = [i]
        for j, right in enumerate(b, 1):
            current.append(min(current[-1] + 1, previous[j] + 1,
                               previous[j - 1] + (left != right)))
        previous = current
    return previous[-1]


def same_dialog(a, b):
    # Punctuation is unreliable during typewriter animation; retain it in output/evaluation.
    # Small-kana confusion must not create new dialogue boundaries. Never alter output text.
    kana = str.maketrans("ぁぃぅぇぉっゃゅょゎァィゥェォッャュョヮヵヶ", "あいうえおつやゆよわアイウエオツヤユヨワカケ")
    a, b = ("".join(c for c in normalized(t).translate(kana) if not unicodedata.category(c).startswith("P"))
            for t in (a, b))
    if not a or not b:
        return False
    shortest = min(len(a), len(b))
    return (a.startswith(b) or b.startswith(a) or SequenceMatcher(None, a, b).ratio() >= .78 or
            (shortest >= 4 and SequenceMatcher(None, a[:shortest], b[:shortest]).ratio() >= .8))


def assemble(samples, interval):
    """Keep temporal gaps; combine typewriter prefixes, never global deduplication."""
    groups = []
    active = []
    pending = []
    for sample in samples:
        sample = {**sample, "speaker": clean_speaker(sample["speaker"])}
        if not sample["body"]:
            continue
        # This first probe handles named dialogue only. Background signs are not dialogue.
        if not active and not sample["speaker"]:
            continue
        if active:
            best = max(active, key=lambda x: len(normalized(x["body"])))
            gap = sample["time"] - active[-1]["time"] > interval * 2.1
            speaker_a, speaker_b = normalized(sample["speaker"]), normalized(best["speaker"])
            names_differ = (speaker_a and speaker_b and speaker_a != speaker_b and
                            distance(speaker_a, speaker_b) / max(len(speaker_a), len(speaker_b)) > .25)
            text_a, text_b = normalized(sample["body"]), normalized(best["body"])
            stable_text = min(len(text_a), len(text_b)) >= 8 and SequenceMatcher(None, text_a, text_b).ratio() >= .78
            # A misread name must not split an otherwise unchanged full sentence.
            changed_speaker = names_differ and not stable_text
            if gap:
                groups.append(active)
                active = []
                pending = []
                if not sample["speaker"]:
                    continue
            elif changed_speaker or not same_dialog(sample["body"], best["body"]):
                pending.append(sample)
                if len(pending) < 2:
                    continue
                groups.append(active)
                active = pending
                pending = []
                continue
            else:
                pending = []
        active.append(sample)
    if active:
        groups.append(active)
    result = []
    for group in groups:
        # Repeated full readings win over one anomalously long OCR reading.
        counts = Counter(normalized(x["body"]) for x in group)
        best = max(group, key=lambda x: (len(normalized(x["body"])) *
                   min(2, counts[normalized(x["body"])]) ** .3, x["confidence"]))
        speaker = Counter(x["speaker"] for x in group if x["speaker"]).most_common(1)
        result.append({"start": group[0]["time"], "end": group[-1]["time"] + interval,
                       "speaker": speaker[0][0] if speaker else "", "body": best["body"],
                       "confidence": best["confidence"], "sample_count": len(group),
                       "evidence_time": best["time"], "needs_review": True})
    for index in range(len(result) - 1):
        result[index]["end"] = min(result[index]["end"], result[index + 1]["start"])
    return result


def reassemble(args):
    source = Path(args.recognition)
    report = json.loads(source.read_text(encoding="utf-8"))
    # Early probes hashed the entire model directory. Preserve that inventory,
    # but identify only models selected by the recorded recognizer as loaded models.
    if "models" in report:
        selected = {"ch_PP-OCRv5_det_mobile.onnx",
                    "japan_PP-OCRv4_rec_mobile.onnx" if report.get("recognizer") == "japan-v4"
                    else "ch_PP-OCRv5_rec_server.onnx"}
        report["model_directory_files"] = report["models"]
        report["models"] = [m for m in report["models"] if m["file"] in selected]
    samples = [json.loads(line) for line in (source.parent / "samples.jsonl").read_text(encoding="utf-8").splitlines()]
    report["dialogs"] = assemble(samples, report["sample_interval"])
    for item in report["dialogs"]:
        item["end"] = min(item["end"], report["processed_seconds"])
    report["reassembled_from"] = str(source.resolve())
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=False)
    write_json(output / "recognition.json", report)
    (output / "samples.jsonl").write_text((source.parent / "samples.jsonl").read_text(encoding="utf-8"), encoding="utf-8")
    export(output, report["dialogs"])


def write_json(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


def inventory(args):
    items = []
    for video in sorted(Path(args.directory).rglob("*.mp4")):
        info = json.loads(subprocess.check_output([
            "ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries",
            "format=duration:stream=width,height,r_frame_rate", "-of", "json", str(video)], encoding="utf-8"))
        item = {"video": str(video.resolve()), "bytes": video.stat().st_size,
                "duration_seconds": float(info["format"]["duration"]), **info["streams"][0]}
        script = video.with_suffix(".json")
        if script.exists():
            data = json.loads(script.read_text(encoding="utf-8-sig"))
            talks = data.get("TalkData", [])
            effects = data.get("SpecialEffectData", [])
            item.update({"script_dialogs": len(talks),
                         "dialog_snippets": sum(x["Action"] == 1 for x in data.get("Snippets", [])),
                         "unnamed_dialogs": sum(not x.get("WindowDisplayName") for x in talks),
                         "consecutive_same_speaker": sum(a.get("WindowDisplayName") == b.get("WindowDisplayName")
                                                         for a, b in zip(talks, talks[1:])),
                         "banners": sum(x.get("EffectType") == 8 for x in effects),
                         "markers": sum(x.get("EffectType") == 18 for x in effects),
                         "translation_present": video.with_suffix(".txt").exists()})
        items.append(item)
    write_json(args.output, items)
    print(f"Inventoried {len(items)} videos")


def stamp(seconds, ass=False):
    units = 100 if ass else 1000
    ticks = round(seconds * units)
    whole, fraction = divmod(ticks, units)
    minutes, second = divmod(whole, 60)
    hour, minute = divmod(minutes, 60)
    return (f"{hour}:{minute:02}:{second:02}.{fraction:02}" if ass else
            f"{hour:02}:{minute:02}:{second:02},{fraction:03}")


def export(output, dialogs):
    srt, ass = [], ["[Script Info]", "ScriptType: v4.00+", "PlayResX: 1920", "PlayResY: 1080",
                    "[V4+ Styles]", "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding",
                    "Style: Default,Yu Gothic,42,&H00FFFFFF,&H00FFFFFF,&H00000000,&H80000000,0,0,0,0,100,100,0,0,1,2,0,2,80,80,60,1",
                    "[Events]", "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text"]
    for i, item in enumerate(dialogs, 1):
        text = (item["speaker"] + "：" if item["speaker"] else "") + item["body"]
        srt.append(f"{i}\n{stamp(item['start'])} --> {stamp(item['end'])}\n{text}\n")
        # OCR is untrusted text, never interpret it as ASS override tags.
        safe = text.replace("\\", "＼").replace("{", "｛").replace("}", "｝").replace("\n", "\\N")
        ass.append(f"Dialogue: 0,{stamp(item['start'], True)},{stamp(item['end'], True)},Default,,0,0,0,,{safe}")
    (output / "subtitles.srt").write_text("\n".join(srt), encoding="utf-8-sig")
    (output / "subtitles.ass").write_text("\n".join(ass), encoding="utf-8-sig")


def evidence(args):
    """Create a local contact sheet from already recognized evidence timestamps."""
    import cv2
    source = Path(args.recognition)
    report = json.loads(source.read_text(encoding="utf-8"))
    images = source.parent / "evidence"
    images.mkdir(exist_ok=True)
    capture = cv2.VideoCapture(report["video"])
    if not capture.isOpened():
        raise ValueError("Cannot open evidence video")
    cards = []
    try:
        for index, item in enumerate(report["dialogs"], 1):
            capture.set(cv2.CAP_PROP_POS_MSEC, item["evidence_time"] * 1000)
            ok, frame = capture.read()
            if not ok:
                raise RuntimeError(f"Cannot read evidence {index}")
            height, width = frame.shape[:2]
            x0, y0, x1, y1, _ = report["roi"]
            crop = frame[int(y0 * height):int(y1 * height), int(x0 * width):int(x1 * width)]
            encoded, buffer = cv2.imencode(".jpg", crop, [cv2.IMWRITE_JPEG_QUALITY, 90])
            if not encoded:
                raise RuntimeError("JPEG encoding failed")
            filename = f"{index:04}.jpg"
            (images / filename).write_bytes(buffer.tobytes())
            cards.append(f'<article><h2>{index} · {html.escape(item["speaker"])}</h2>'
                         f'<p>{stamp(item["start"])} → {stamp(item["end"])} · '
                         f'OCR {item["confidence"]:.3f} · 待校对</p>'
                         f'<img src="evidence/{filename}" alt="对白截图 {index}">'
                         f'<pre>{html.escape(item["body"])}</pre></article>')
    finally:
        capture.release()
    page = ('<!doctype html><html lang="zh-CN"><meta charset="utf-8">'
            '<title>纯视频识别校对</title><style>body{font:16px system-ui;max-width:1100px;'
            'margin:32px auto;background:#f4f5f7;color:#19202b}article{background:white;'
            'padding:20px;margin:20px 0;border-radius:12px}img{max-width:100%}'
            'pre{white-space:pre-wrap;font:20px system-ui}</style><h1>纯视频识别校对</h1>'
            '<p>时间轴为抽样估计；OCR 高置信度也可能有错。修改 recognition.json 后可重新导出。</p>'
            + "".join(cards) + '</html>')
    (source.parent / "review.html").write_text(page, encoding="utf-8")


def export_saved(args):
    source = Path(args.recognition)
    report = json.loads(source.read_text(encoding="utf-8"))
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=False)
    for item in report["dialogs"]:
        if not (math.isfinite(item["start"]) and math.isfinite(item["end"]) and
                0 <= item["start"] < item["end"]):
            raise ValueError("Every dialogue must have finite, increasing, nonnegative times")
    write_json(output / "recognition.json", report)
    export(output, report["dialogs"])


def recognize(args):
    import cv2
    from rapidocr import RapidOCR, OCRVersion, ModelType, LangRec
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=False)
    started = time.perf_counter()
    capture = cv2.VideoCapture(str(Path(args.video).resolve()))
    if not capture.isOpened():
        raise ValueError("Cannot open video")
    fps = capture.get(cv2.CAP_PROP_FPS)
    width, height = (int(capture.get(prop)) for prop in
                     (cv2.CAP_PROP_FRAME_WIDTH, cv2.CAP_PROP_FRAME_HEIGHT))
    frames = int(capture.get(cv2.CAP_PROP_FRAME_COUNT))
    if fps <= 0 or frames <= 0:
        raise ValueError("Video must have a known positive frame rate and frame count")
    # Explicit initial layout presets, not an automatic UI detector.
    layout = args.layout
    if layout == "auto":
        layout = "tablet" if width / height < 1.9 else "wide"
    split_default = 1 - .12 * min(width / height, 16 / 9)
    roi = args.roi or ([.10, split_default - .05, .90, .95, split_default] if layout == "tablet" else
                       [.17, .72, .86, .92, .79])
    x0, y0, x1, y1, split = roi
    if not (0 <= x0 < x1 <= 1 and 0 <= y0 < split < y1 <= 1):
        raise ValueError("ROI must be normalized x0 y0 x1 y1 name/body-split")
    engine = RapidOCR(params={
        "Global.model_root_dir": str(Path(args.models).resolve()), "Global.use_cls": False,
        "Det.ocr_version": OCRVersion.PPOCRV5, "Det.model_type": ModelType.MOBILE,
        "Det.limit_type": "max", "Det.limit_side_len": 1536,
        "Rec.ocr_version": OCRVersion.PPOCRV4 if args.recognizer == "japan-v4" else OCRVersion.PPOCRV5,
        "Rec.model_type": ModelType.MOBILE if args.recognizer == "japan-v4" else ModelType.SERVER,
        "Rec.lang_type": LangRec.JAPAN if args.recognizer == "japan-v4" else LangRec.CH,
        "EngineConfig.onnxruntime.intra_op_num_threads": 4})
    samples = []
    step = max(1, round(args.interval * fps))
    actual_interval = step / fps
    limit = min(frames, round(args.max_seconds * fps)) if args.max_seconds else frames
    try:
        with (output / "samples.jsonl").open("w", encoding="utf-8") as stream:
            for frame_index in range(limit):
                if not capture.grab():
                    raise RuntimeError(f"Unexpected video decode failure at frame {frame_index}")
                if frame_index % step:
                    continue
                ok, frame = capture.retrieve()
                if not ok:
                    raise RuntimeError(f"Cannot retrieve frame {frame_index}")
                crop = frame[int(y0 * height):int(y1 * height), int(x0 * width):int(x1 * width)]
                # Bound detector cost without changing the original video.
                scale = min(1, 1536 / crop.shape[1])
                crop = cv2.resize(crop, None, fx=scale, fy=scale)
                prediction = engine(crop)
                name, body, scores = [], [], []
                if prediction.txts:
                    rows = sorted(zip(prediction.boxes, prediction.txts, prediction.scores),
                                  key=lambda row: (float(row[0][:, 1].mean()), float(row[0][:, 0].min())))
                    lines = []
                    for box, text, score in rows:
                        cy = float(box[:, 1].mean())
                        if cy < (split - y0) * height * scale:
                            if float(box[:, 0].mean()) < crop.shape[1] * .45:
                                name.append(text)
                        else:
                            if not lines or abs(cy - lines[-1][0]) > float(box[:, 1].max() - box[:, 1].min()) * .5:
                                lines.append((cy, []))
                            lines[-1][1].append((float(box[:, 0].min()), text))
                            scores.append(float(score))
                    body = ["".join(text for _, text in sorted(parts)) for _, parts in lines]
                sample = {"frame": frame_index, "time": frame_index / fps,
                          "speaker": "".join(name), "body": "\n".join(body),
                          "confidence": min(scores) if scores else 0}
                stream.write(json.dumps(sample, ensure_ascii=False) + "\n")
                stream.flush()
                samples.append(sample)
                if len(samples) % 40 == 0:
                    print(f"OCR {frame_index / fps:.1f}/{limit / fps:.1f}s", flush=True)
    finally:
        capture.release()
    dialogs = assemble(samples, actual_interval)
    for item in dialogs:
        item["end"] = min(item["end"], limit / fps)
    models = []
    model_names = ["ch_PP-OCRv5_det_mobile.onnx",
                   "japan_PP-OCRv4_rec_mobile.onnx" if args.recognizer == "japan-v4" else "ch_PP-OCRv5_rec_server.onnx"]
    for name in model_names:
        with (Path(args.models) / name).open("rb") as model:
            models.append({"file": name, "sha256": hashlib.file_digest(model, "sha256").hexdigest()})
    report = {"schema": 1, "mode": "video-only", "video": str(Path(args.video).resolve()),
              "width": width, "height": height, "fps": fps, "frames": frames,
              "processed_seconds": limit / fps, "layout": layout, "roi": roi,
              "sample_interval": actual_interval, "samples": len(samples), "recognizer": args.recognizer,
              "elapsed_seconds": time.perf_counter() - started, "dialogs": dialogs,
              "models": models, "versions": {p: importlib.metadata.version(p) for p in
                                               ("rapidocr", "onnxruntime", "opencv-python-headless")},
              "timing": "sampled estimates, not frame-refined; all results require review",
              "script_used": False, "translation_used": False}
    write_json(output / "recognition.json", report)
    export(output, dialogs)
    print(json.dumps({"dialogs": len(dialogs), "seconds": report["elapsed_seconds"]}), flush=True)


def evaluate(args):
    # Only this command can open a script. No reference information feeds back into OCR.
    report = json.loads(Path(args.recognition).read_text(encoding="utf-8"))
    script = json.loads(Path(args.script).read_text(encoding="utf-8-sig"))
    reference = [{"speaker": d["WindowDisplayName"], "body": d["Body"]} for d in script["TalkData"]]
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
            a, b = normalized(reference[i - 1]["body"]), normalized(observed[j - 1]["body"])
            error = distance(a, b) / max(len(a), len(b), 1)
            costs[i][j], moves[i, j] = min((costs[i - 1][j - 1] + 2 * error, "pair"),
                                          (costs[i - 1][j] + 1, "missing"),
                                          (costs[i][j - 1] + 1, "extra"))
    pairs = []
    i, j = n, m
    while i or j:
        move = moves[i, j]
        pairs.append({"reference_index": i - 1 if move != "extra" else None,
                      "observed_index": j - 1 if move != "missing" else None, "kind": move})
        i -= move != "extra"
        j -= move != "missing"
    pairs.reverse()
    edits = total = exact = speakers = matched = 0
    baseline = None
    if args.state:
        baseline = json.loads(Path(args.state).read_text(encoding="utf-8"))
        if Path(baseline["VideoFilePath"]).resolve() != Path(report["video"]).resolve():
            raise ValueError("State belongs to another video")
        if len(baseline["Dialog"]["FrameSets"]) != n:
            raise ValueError("State has incomplete dialogs; cannot assume frame-set order matches script")
    for pair in pairs:
        ri, oi = pair["reference_index"], pair["observed_index"]
        a = normalized(reference[ri]["body"]) if ri is not None else ""
        b = normalized(observed[oi]["body"]) if oi is not None else ""
        edits += distance(a, b)
        total += len(a)
        pair["reference"] = reference[ri] if ri is not None else None
        pair["observed"] = observed[oi] if oi is not None else None
        if ri is not None and oi is not None:
            matched += 1
            exact += a == b
            speakers += normalized(reference[ri]["speaker"]) == normalized(observed[oi]["speaker"])
            pair["character_error_rate"] = distance(a, b) / max(1, len(a))
            if baseline:
                sets = baseline["Dialog"]["FrameSets"]
                fs = sets[ri]
                if fs and fs["Frames"]:
                    start, end = fs["Frames"][0]["Index"] / report["fps"], fs["Frames"][-1]["Index"] / report["fps"]
                    pair["reference_start"] = start
                    pair["reference_end"] = end
                    pair["start_delta_seconds"] = observed[oi]["start"] - start
                    pair["end_delta_seconds"] = observed[oi]["end"] - end
    result = {"reference_dialogs": n, "observed_dialogs": m, "aligned_pairs": matched,
              "missing": sum(p["kind"] == "missing" for p in pairs),
              "extra": sum(p["kind"] == "extra" for p in pairs),
              "exact_text_pairs": exact, "exact_speaker_pairs": speakers,
              "normalized_character_error_rate": edits / max(1, total),
              "normalization": "NFKC and remove whitespace; punctuation retained",
              "timing_reference": "script-assisted output is a comparator, not manually annotated ground truth",
              "pairs": pairs}
    write_json(args.output, result)
    print(json.dumps({k: v for k, v in result.items() if k != "pairs"}))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    rec = sub.add_parser("recognize")
    rec.add_argument("video")
    rec.add_argument("output", help="New output directory; never overwrite an existing run")
    rec.add_argument("--models", required=True)
    rec.add_argument("--layout", choices=("auto", "tablet", "wide"), default="auto")
    rec.add_argument("--recognizer", choices=("server-v5", "japan-v4"), default="server-v5")
    rec.add_argument("--roi", nargs=5, type=float, metavar=("X0", "Y0", "X1", "Y1", "SPLIT"))
    rec.add_argument("--interval", type=float, default=.25)
    rec.add_argument("--max-seconds", type=float)
    rec.set_defaults(run=recognize)
    ev = sub.add_parser("evaluate")
    ev.add_argument("recognition")
    ev.add_argument("script")
    ev.add_argument("output")
    ev.add_argument("--state", help="Optional existing script-assisted ProcessingState")
    ev.set_defaults(run=evaluate)
    rebuild = sub.add_parser("reassemble")
    rebuild.add_argument("recognition")
    rebuild.add_argument("output")
    rebuild.set_defaults(run=reassemble)
    inv = sub.add_parser("inventory")
    inv.add_argument("directory")
    inv.add_argument("output")
    inv.set_defaults(run=inventory)
    review = sub.add_parser("evidence")
    review.add_argument("recognition")
    review.set_defaults(run=evidence)
    saved = sub.add_parser("export")
    saved.add_argument("recognition")
    saved.add_argument("output")
    saved.set_defaults(run=export_saved)
    args = parser.parse_args()
    if args.command == "recognize" and (not math.isfinite(args.interval) or args.interval <= 0 or
                                       (args.max_seconds is not None and
                                        (not math.isfinite(args.max_seconds) or args.max_seconds <= 0))):
        parser.error("interval and max-seconds must be positive")
    args.run(args)


if __name__ == "__main__":
    main()
