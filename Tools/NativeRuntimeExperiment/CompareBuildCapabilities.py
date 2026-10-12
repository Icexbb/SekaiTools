"""Compare actual loaded build inventories; this does not prove ABI compatibility."""
import hashlib
import json
import pathlib
import re
import sys


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def inspect(directory):
    report_path = directory / "capabilities.json"
    build_path = directory / "build.txt"
    report = json.loads(report_path.read_text(encoding="utf-8-sig"))
    libraries = report["LoadedNativeLibraries"]
    if report["LoadedNativeError"] or len(libraries) != 1:
        raise ValueError("Exactly one verified native module required")
    native = libraries[0]
    if sha(pathlib.Path(native["Path"])) != native["Hash"]:
        raise ValueError("Loaded native file changed")
    build = build_path.read_text(encoding="utf-8-sig")
    match = re.search(r"^\s*To be built:\s+(.+)$", build, re.MULTILINE)
    if not match:
        raise ValueError("Missing module inventory")
    modules = sorted(set(match.group(1).split()))
    features = {}
    for name in ("WEBP", "TIFF", "JPEG 2000", "DirectShow", "Media Foundation", "Intel IPP"):
        feature = re.search(r"^\s*" + re.escape(name) + r":\s+(.+)$", build, re.MULTILINE)
        features[name] = feature.group(1).strip() if feature else None
    return {"NativeHash": native["Hash"], "ReportHash": sha(report_path),
            "BuildInformationHash": sha(build_path), "Modules": modules, "Features": features}


original_path, candidate_path, destination = map(pathlib.Path, sys.argv[1:4])
original, candidate = inspect(original_path), inspect(candidate_path)
result = {"Original": original, "Candidate": candidate,
          "MissingModules": sorted(set(original["Modules"]) - set(candidate["Modules"])),
          "AddedModules": sorted(set(candidate["Modules"]) - set(original["Modules"])),
          "FullSdkCompatibilityProven": False, "NoPerformanceConclusion": True}
with destination.open("x", encoding="utf-8") as stream:
    json.dump(result, stream, ensure_ascii=False, indent=2)
print(json.dumps({"OriginalModules": len(original["Modules"]),
                  "CandidateModules": len(candidate["Modules"]),
                  "MissingModules": len(result["MissingModules"])}))
