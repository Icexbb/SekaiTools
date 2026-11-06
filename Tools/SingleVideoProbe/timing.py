"""Video-only dialogue timing experiment; no OCR or script dependencies."""
import argparse
import json
import math
from pathlib import Path
import time

import cv2
import numpy as np

from probe import write_json


def text_mask(image):
    """Extract small, bright, near-neutral glyph components, rather than read text."""
    channels = image.astype(np.int16)
    bright = ((channels.min(axis=2) > 175) &
              (channels.max(axis=2) - channels.min(axis=2) < 65)).astype(np.uint8) * 255
    count, labels, stats, _ = cv2.connectedComponentsWithStats(bright)
    valid = np.zeros(count, dtype=np.uint8)
    for index in range(1, count):
        _, _, width, height, area = stats[index]
        if 2 <= width <= 70 and 4 <= height <= 65 and 5 <= area <= 2500:
            valid[index] = 255
    return valid[labels]


def retained(previous, current):
    count = cv2.countNonZero(previous)
    if count == 0:
        return 1.
    # Tolerate antialiasing changes and small dialog movement.
    expanded = cv2.dilate(current, np.ones((5, 5), np.uint8))
    return cv2.countNonZero(cv2.bitwise_and(previous, expanded)) / count


def has_name_line(image):
    edges = cv2.Canny(cv2.cvtColor(image, cv2.COLOR_BGR2GRAY), 10, 30)
    horizontal = cv2.morphologyEx(edges, cv2.MORPH_OPEN, np.ones((1, 120), np.uint8))
    _, _, stats, _ = cv2.connectedComponentsWithStats(horizontal)
    return any(x < 256 and y >= image.shape[0] * .4 and width >= 120
               for x, y, width, _, _ in stats[1:])


def run(args):
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=False)
    capture = cv2.VideoCapture(str(Path(args.video).resolve()))
    if not capture.isOpened():
        raise ValueError("Cannot open video")
    fps = capture.get(cv2.CAP_PROP_FPS)
    frames = int(capture.get(cv2.CAP_PROP_FRAME_COUNT))
    width, height = (int(capture.get(prop)) for prop in
                     (cv2.CAP_PROP_FRAME_WIDTH, cv2.CAP_PROP_FRAME_HEIGHT))
    split = 1 - .12 * min(width / height, 16 / 9)
    roi = args.roi or ([.10, split - .05, .90, .95, split] if width / height < 1.9
                       else [.17, .72, .86, .92, .79])
    x0, y0, x1, y1, split = roi
    if not (0 <= x0 < x1 <= 1 and 0 <= y0 < split < y1 <= 1):
        raise ValueError("Invalid normalized ROI")
    if not math.isfinite(fps) or fps <= 0 or frames <= 0 or not math.isfinite(args.interval) or args.interval <= 0:
        raise ValueError("Video frame rate/count and sample interval must be positive")
    step = max(1, round(args.interval * fps))
    interval = step / fps
    menu = None
    if args.menu_template:
        source = cv2.imread(args.menu_template, cv2.IMREAD_UNCHANGED)
        if source is None or source.shape[2] != 4:
            raise ValueError("Menu template must be a BGRA image")
        size = int(min(width * .0417, height * .0741))
        menu = cv2.resize(source, (size, size))
    started = time.perf_counter()
    dialogs, observations = [], []
    active = None
    previous = None
    pending_reset = None
    last_visible = None

    def finish(end):
        nonlocal active, pending_reset
        if active is not None:
            active["end"] = end
            if end - active["start"] >= .25:
                dialogs.append(active)
        active = None
        pending_reset = None

    try:
        for frame_index in range(frames):
            if not capture.grab():
                raise RuntimeError(f"Decode failure at frame {frame_index}")
            if frame_index % step:
                continue
            ok, frame = capture.retrieve()
            if not ok:
                raise RuntimeError(f"Cannot retrieve frame {frame_index}")
            now = frame_index / fps
            crop = frame[int(y0 * height):int(y1 * height), int(x0 * width):int(x1 * width)]
            scale = 1280 / crop.shape[1]
            crop = cv2.resize(crop, None, fx=scale, fy=scale)
            boundary = round((split - y0) * height * scale)
            name = text_mask(crop[:boundary, :round(crop.shape[1] * .45)])
            body = text_mask(crop[boundary:])
            pixels = cv2.countNonZero(body)
            menu_score = 1.
            if menu is not None:
                size = menu.shape[0]
                region = cv2.cvtColor(frame[:size * 2, width - size * 3:], cv2.COLOR_BGR2GRAY)
                result = cv2.matchTemplate(region, cv2.cvtColor(menu, cv2.COLOR_BGRA2GRAY),
                                           cv2.TM_CCOEFF_NORMED, mask=menu[:, :, 3])
                menu_score = float(np.max(np.nan_to_num(result, nan=0, posinf=0, neginf=0)))
            line_visible = has_name_line(crop[:boundary]) if menu is not None else True
            visible = cv2.countNonZero(name) >= 30 and pixels >= 35 and menu_score >= .85 and line_visible
            overlap = retained(previous, body) if previous is not None else 1.
            reset = visible and previous is not None and overlap < .65
            observations.append({"frame": frame_index, "time": now, "visible": visible,
                                 "body_pixels": pixels, "retained_fraction": overlap,
                                 "menu_score": menu_score, "name_line": line_visible})
            if visible:
                if active is None:
                    active = {"start": now, "end": now, "speaker": "", "body": "",
                              "translation": "", "confidence": 0., "needs_review": True,
                              "evidence_time": now}
                elif reset:
                    # New text replaces the previous glyph pattern; typing only adds pixels.
                    # Keep the first change as the candidate boundary.
                    if pending_reset is None:
                        pending_reset = now
                elif pending_reset is not None and now - pending_reset >= interval * .9:
                    boundary_time = pending_reset
                    finish(boundary_time)
                    active = {"start": boundary_time, "end": now, "speaker": "", "body": "",
                              "translation": "", "confidence": 0., "needs_review": True,
                              "evidence_time": now}
                if active is not None:
                    active["evidence_time"] = now
                previous = body
                last_visible = now
            else:
                # Empty body with a still-visible name is also a dialogue reset.
                if active is not None and last_visible is not None and now - last_visible >= interval * 1.9:
                    finish(last_visible + interval)
                    previous = None
        finish(min(frames / fps, (last_visible + interval) if last_visible is not None else frames / fps))
    finally:
        capture.release()
    report = {"schema": 1, "mode": "timing-only", "video": str(Path(args.video).resolve()),
              "fps": fps, "frames": frames, "width": width, "height": height,
              "processed_seconds": frames / fps, "roi": roi, "sample_interval": interval,
              "elapsed_seconds": time.perf_counter() - started, "dialogs": dialogs,
              "script_used": False, "translation_used": False, "ocr_used": False,
              "dialogue_gate": "menu-and-name-line" if menu is not None else "glyphs-only",
              "timing": "visual-change candidates; needs manual review"}
    write_json(output / "recognition.json", report)
    with (output / "visual-samples.jsonl").open("w", encoding="utf-8") as stream:
        for item in observations:
            stream.write(json.dumps(item) + "\n")
    print(json.dumps({"dialogs": len(dialogs), "seconds": report["elapsed_seconds"]}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("video")
    parser.add_argument("output")
    parser.add_argument("--interval", type=float, default=1 / 12)
    parser.add_argument("--roi", nargs=5, type=float)
    parser.add_argument("--menu-template", help="Existing menu-107px.png; enables dialogue UI gating")
    run(parser.parse_args())
