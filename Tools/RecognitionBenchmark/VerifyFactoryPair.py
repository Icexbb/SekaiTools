"""Compare a factory qualification with a frozen ordinary-capture result."""
import hashlib
import json
import pathlib
import sys


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


reference, candidate = map(pathlib.Path, sys.argv[1:3])
read = lambda path: json.loads(path.read_text(encoding="utf-8-sig"))
r, c = (read(path / "run-1.json") for path in (reference, candidate))
ownership = read(candidate / "ownership-1.json")
assert c["ProcessorFactoryCapture"] and c["NativePeriodicColorCapture"]
assert c["PreviewFrames"] > 0 and c["NonColorPreviewFrames"] == 0
assert c["ActualDecoderThreads"] == 16 and c["DecodeAcceleration"] == "None"
identity = (f'PeriodicColor:FFMPEG:None:16:{c["PeriodicColorInterval"]}:'
            f'{c["PeriodicColorNativeHash"]}:{c["PeriodicColorPluginHash"]}')
assert c["CaptureIdentity"] == ownership["Identity"] == identity
assert ownership["CaptureDisposed"] and ownership["SessionDisposed"]
for library in c["NativeLibraries"]:
    assert digest(pathlib.Path(library["Path"])) == library["Hash"]
libraries = {pathlib.Path(x["Path"]).name.lower(): x["Hash"] for x in c["NativeLibraries"]}
assert libraries["cvextern.dll"] == c["PeriodicColorNativeHash"]
assert libraries["opencv_videoio_ffmpeg500_64.dll"] == c["PeriodicColorPluginHash"]
states = []
for path, report in ((reference, r), (candidate, c)):
    assert digest(path / "state-1.json") == report["StateHash"]
    assert digest(path / "subtitle-1.ass") == report["SubtitleHash"]
    states.append(read(path / "state-1.json"))
assert states[0]["Metadata"].get("CaptureIdentity") is None
assert states[1]["Metadata"]["CaptureIdentity"] == identity
# Normalize only the separately verified implementation identity, never ConfigHash.
for state in states:
    state["Metadata"].pop("CaptureIdentity", None)
assert states[0] == states[1], "Full processing state differs"
assert (reference / "subtitle-1.ass").read_bytes() == (candidate / "subtitle-1.ass").read_bytes()
assert r["StopReason"] == c["StopReason"]
assert r["Performance"]["FrameCount"] == c["Performance"]["FrameCount"]
print(json.dumps({"VerificationPassed": True, "FullStateAndSubtitleEqual": True,
                  "CaptureIdentity": identity, "ProcessorReleasedSession": True,
                  "NoPerformanceConclusion": True}))
