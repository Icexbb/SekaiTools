"""Read PE export names without loading either Windows runtime."""
import hashlib
import json
import struct
import sys
from pathlib import Path


def inspect(path):
    data = path.read_bytes()
    assert data[:2] == b"MZ"
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    assert data[pe:pe + 4] == b"PE\0\0"
    machine, sections = struct.unpack_from("<HH", data, pe + 4)
    optional_size = struct.unpack_from("<H", data, pe + 20)[0]
    optional = pe + 24
    assert machine == 0x8664 and struct.unpack_from("<H", data, optional)[0] == 0x20B, "Expected x64 PE32+"
    table = optional + optional_size

    def offset(rva):
        for i in range(sections):
            virtual_size, address, raw_size, raw = struct.unpack_from("<IIII", data, table + i * 40 + 8)
            if address <= rva < address + max(virtual_size, raw_size):
                assert rva - address < raw_size, "RVA has no file bytes"
                return raw + rva - address
        raise ValueError(f"Unmapped RVA {rva:x}")

    def string(rva):
        start = offset(rva)
        return data[start:data.index(b"\0", start)].decode("ascii")

    export_rva = struct.unpack_from("<I", data, optional + 112)[0]
    names = set()
    if export_rva:
        directory = offset(export_rva)
        count = struct.unpack_from("<I", data, directory + 24)[0]
        name_table = offset(struct.unpack_from("<I", data, directory + 32)[0])
        assert count < 100000
        names = {string(struct.unpack_from("<I", data, name_table + 4 * i)[0]) for i in range(count)}
        assert len(names) == count, "Duplicate export names"
    dependencies = []
    import_rva = struct.unpack_from("<I", data, optional + 120)[0]
    if import_rva:
        descriptor = offset(import_rva)
        while any(data[descriptor:descriptor + 20]):
            dependencies.append(string(struct.unpack_from("<I", data, descriptor + 12)[0]))
            descriptor += 20
    return {"Path": str(path.resolve()), "Hash": hashlib.sha256(data).hexdigest().upper(),
            "ExportNames": sorted(names), "ImportedDlls": sorted(dependencies)}


if len(sys.argv) != 4:
    raise SystemExit("CompareExports.py <reference-dll> <candidate-dll> <new-report>")
if not __debug__:
    raise RuntimeError("Do not use Python -O")
reference, candidate, report = map(Path, sys.argv[1:])
if report.exists():
    raise RuntimeError("Do not overwrite evidence")
a, b = inspect(reference), inspect(candidate)
missing = sorted(set(a["ExportNames"]) - set(b["ExportNames"]))
added = sorted(set(b["ExportNames"]) - set(a["ExportNames"]))
result = {"Reference": a, "Candidate": b, "MissingExports": missing, "AddedExports": added,
          "ExportNamesPreserved": not missing, "FullAbiCompatibilityQualified": False}
report.write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps({"ReferenceExports": len(a["ExportNames"]), "CandidateExports": len(b["ExportNames"]),
                  "MissingCount": len(missing), "AddedCount": len(added), "MissingExamples": missing[:12]}))
