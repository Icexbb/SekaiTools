"""Exercise rejection paths without altering recorded qualification evidence."""
import json
import shutil
import sys
from pathlib import Path
from VerifyPeriodicColorPair import digest, verify

reference, candidate, root = map(Path, sys.argv[1:])
assert not root.exists(), "Use a fresh fixture directory"
root.mkdir(parents=True)
for name in ("state", "ass", "preview_count", "preview_channels", "interval", "runtime"):
    target = root / name
    target.mkdir()
    for file in candidate.glob("*-1.*"):
        shutil.copy2(file, target / file.name)
    report_path = target / "run-1.json"
    report = json.loads(report_path.read_text(encoding="utf-8"))
    if name == "state":
        path = target / "state-1.json"
        state = json.loads(path.read_text(encoding="utf-8"))
        state["FrameIndex"] += 1
        path.write_text(json.dumps(state), encoding="utf-8")
        report["StateHash"] = digest(path)
    elif name == "ass":
        path = target / "subtitle-1.ass"
        path.write_bytes(path.read_bytes() + b"\n; altered\n")
        report["SubtitleHash"] = digest(path)
    elif name == "preview_count":
        report["PreviewFrames"] = 0
    elif name == "preview_channels":
        report["NonColorPreviewFrames"] = 1
    elif name == "interval":
        report["PeriodicColorInterval"] += 1
    else:
        report["NativeLibraries"][0]["Hash"] = "0" * 64
    report_path.write_text(json.dumps(report), encoding="utf-8")
    try:
        verify(reference, target, require_native_identity=True)
    except AssertionError:
        print(f"Rejected: {name}")
    else:
        raise RuntimeError(f"Corrupt fixture accepted: {name}")
