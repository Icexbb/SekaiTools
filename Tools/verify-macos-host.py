#!/usr/bin/env python3
"""Exercise the published Host over real UTF-8 JSON Lines without loading OpenCV."""
import argparse
import json
import pathlib
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("host", type=pathlib.Path, help="Published SekaiToolsHost executable")
    parser.add_argument("--download", action="store_true", help="Also refresh event catalog and download one real script")
    args = parser.parse_args()
    host = args.host.resolve()

    def request(command, parameters=None, expected_exit=0, timeout=30):
        payload = json.dumps({"version": 1, "command": command, "args": parameters or {}}, ensure_ascii=False)
        process = subprocess.run([str(host)], input=payload + "\n", capture_output=True,
                                 encoding="utf-8", timeout=timeout)
        assert process.returncode == expected_exit, process.stderr
        lines = process.stdout.splitlines()
        assert len(lines) == 1, process.stdout
        response = json.loads(lines[0])
        assert response["version"] == 1, response
        return response

    capabilities = request("capabilities")
    assert capabilities["type"] == "result", capabilities
    assert capabilities["data"]["sources"], capabilities
    print(json.dumps(capabilities["data"], ensure_ascii=False))
    assert request("unsupported", expected_exit=1)["type"] == "error"
    with tempfile.TemporaryDirectory(prefix="sekaitools-mac-probe-") as directory:
        script = pathlib.Path(directory) / "中文剧本.json"
        translation = pathlib.Path(directory) / "译文.txt"
        script.write_text(json.dumps({
            "ScenarioId": "mac-probe",
            "Snippets": [{"Action": 1}],
            "TalkData": [{"WindowDisplayName": "原名", "Body": "原文", "TalkCharacters": [], "Voices": []}],
            "SpecialEffectData": [],
        }, ensure_ascii=False), encoding="utf-8")
        loaded = request("translation.load", {"script": str(script)})
        assert loaded["type"] == "result", loaded
        rows = loaded["data"]
        assert len(rows) == 1 and rows[0]["original"] == "原文", rows
        rows[0].update(speaker="译名", content="第一行\n@第二行")
        saved = request("translation.save", {"script": str(script), "output": str(translation), "rows": rows})
        assert saved["type"] == "result", saved
        reloaded = request("translation.load", {"script": str(script), "translation": str(translation)})
        assert reloaded["data"] == rows, reloaded
        protected = request("translation.save", {"script": str(script), "output": str(script), "rows": rows}, expected_exit=1)
        assert protected["type"] == "error", protected
        # The second export intentionally preserves an explicit empty speaker and body.
        rows[0].update(speaker="", content="")
        request("translation.save", {"script": str(script), "output": str(translation), "rows": rows})
        assert request("translation.load", {"script": str(script), "translation": str(translation)})["data"] == rows
        if args.download:
            catalog = request("download.catalog", {"source": 0, "category": "event", "refresh": True}, timeout=180)
            assert catalog["type"] == "result" and catalog["data"], catalog
            first = catalog["data"][0]
            downloaded = pathlib.Path(directory) / "下载剧本.json"
            saved = request("download.save", {"source": 0, "category": "event", "id": first["id"],
                                              "output": str(downloaded)}, timeout=180)
            assert saved["type"] == "result", saved
            loaded_download = request("translation.load", {"script": str(downloaded)})
            assert loaded_download["type"] == "result" and loaded_download["data"], loaded_download
            print(f"PASS: live event catalog ({len(catalog['data'])} entries), script download and translation import")
    print("PASS: published Host, UTF-8 paths, multiline/empty translation roundtrip, input overwrite protection, structured errors")


if __name__ == "__main__":
    main()
