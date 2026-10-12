"""Check rejection paths against copies, preserving runtime evidence."""
import json
import shutil
import sys
from pathlib import Path
from VerifyPeriodicColorResume import digest, verify

reference, canceled, resumed, root = map(Path, sys.argv[1:])
if root.exists():
    raise RuntimeError("Use a fresh fixture directory")
root.mkdir(parents=True)
for name in ("state_and_history", "subtitle", "checkpoint", "preview_count", "preview_channels", "interval", "release"):
    target = root / name
    shutil.copytree(resumed, target)
    report_path = target / "report.json"
    report = json.loads(report_path.read_text(encoding="utf-8"))
    history = target / Path(report["HistoryPath"]).resolve().relative_to(resumed.resolve())
    report["HistoryPath"] = str(history.resolve())
    if name == "state_and_history":
        path = target / "state.json"
        state = json.loads(path.read_text(encoding="utf-8"))
        state["FrameIndex"] += 1
        encoded = json.dumps(state).encode()
        path.write_bytes(encoded)
        history.write_bytes(encoded)
        report["FrameIndex"] = state["FrameIndex"]
        report["StateHash"] = digest(path)
        report["HistoryHash"] = digest(history)
    elif name == "subtitle":
        path = target / "subtitle.ass"
        path.write_bytes(path.read_bytes() + b"\n; changed\n")
        report["SubtitleHash"] = digest(path)
    elif name == "checkpoint":
        report["SourceCheckpointHash"] = "0" * 64
    elif name == "preview_count":
        report["PreviewFrames"] = 0
    elif name == "preview_channels":
        report["NonColorPreviewFrames"] = 1
    elif name == "interval":
        report["PreviewInterval"] += 1
    else:
        report["CaptureDisposed"] = False
    report_path.write_text(json.dumps(report), encoding="utf-8")
    try:
        verify(reference, canceled, target)
    except AssertionError as error:
        print(f"Rejected {name}: {error}")
    else:
        raise RuntimeError(f"Corrupt fixture accepted: {name}")
