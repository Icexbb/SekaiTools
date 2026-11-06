import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest

from probe import assemble, distance, evaluate, export, normalized, stamp


def sample(time, body, speaker="MEIKO"):
    return {"time": time, "body": body, "speaker": speaker, "confidence": .95}


class ProbeTests(unittest.TestCase):
    def test_typewriter_and_single_frame_error_do_not_split(self):
        values = [sample(0, "……ふう"), sample(.25, "ふう。体が"),
                  sample(.5, "……ふう。体が軽くなった"), sample(.75, "誤認識"),
                  sample(1, "……ふう。体が軽くなった")]
        result = assemble(values, .25)
        self.assertEqual(1, len(result))
        self.assertEqual(0, result[0]["start"])
        self.assertEqual(1.25, result[0]["end"])

    def test_consecutive_same_speaker_and_repeated_line_after_gap(self):
        values = [sample(0, "今日は晴れですね"), sample(.25, "今日は晴れですね"),
                  sample(.5, "明日はどうでしょう"), sample(.75, "明日はどうでしょう"),
                  sample(2, "明日はどうでしょう"), sample(2.25, "明日はどうでしょう")]
        result = assemble(values, .25)
        self.assertEqual(3, len(result))
        self.assertEqual([0, .5, 2], [r["start"] for r in result])

    def test_background_text_is_not_a_named_dialog(self):
        result = assemble([sample(0, "学校の看板", ""), sample(.25, "学校の看板", ""),
                           sample(1, "こんにちは"), sample(1.25, "こんにちは")], .25)
        self.assertEqual(1, len(result))
        self.assertEqual(1, result[0]["start"])

    def test_name_misreading_does_not_split_stable_body(self):
        values = [sample(0, "だから他の学校とかも見て", "瑞希"),
                  sample(.5, "だから他の学校とかも見て", "瑞希"),
                  sample(1, "だから他の学校とかも見て", "瑞肴"),
                  sample(1.5, "だから他の学校とかも見て", "瑞肴"),
                  sample(2, "だから他の学校とかも見て", "瑞希")]
        result = assemble(values, .5)
        self.assertEqual(1, len(result))
        self.assertEqual("瑞希", result[0]["speaker"])

    def test_small_kana_variation_does_not_split_short_dialogue(self):
        result = assemble([sample(0, "えっ？"), sample(.5, "えっ？"),
                           sample(1, "えつ？"), sample(1.5, "えつ？"),
                           sample(2, "えっ？")], .5)
        self.assertEqual(1, len(result))
        self.assertIn(result[0]["body"], ("えっ？", "えつ？"))

    def test_export_escapes_ocr_ass_tags_and_rounds_timestamp(self):
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp)
            export(output, [{"start": 59.9999, "end": 62, "speaker": "A",
                             "body": "{\\pos(1,2)}\n本文"}])
            ass = (output / "subtitles.ass").read_text(encoding="utf-8-sig")
            self.assertNotIn("{\\pos", ass)
            self.assertIn("\\N本文", ass)
            self.assertEqual("00:01:00,000", stamp(59.9999))

    def test_alignment_counts_missing_and_extra_without_modifying_source(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            script = root / "script.json"
            recognition = root / "recognition.json"
            script.write_text(json.dumps({"TalkData": [
                {"WindowDisplayName": "A", "Body": "あいうえお"},
                {"WindowDisplayName": "B", "Body": "かきくけこ"},
                {"WindowDisplayName": "C", "Body": "さしすせそ"}]}), encoding="utf-8")
            recognition.write_text(json.dumps({"dialogs": [
                {"speaker": "A", "body": "あいうえお"},
                {"speaker": "C", "body": "さしすせそ"}]}), encoding="utf-8")
            original = recognition.read_bytes()
            evaluate(SimpleNamespace(recognition=str(recognition), script=str(script),
                                     output=str(root / "comparison.json"), state=None))
            result = json.loads((root / "comparison.json").read_text(encoding="utf-8"))
            self.assertEqual(1, result["missing"])
            self.assertEqual(2, result["exact_text_pairs"])
            self.assertEqual(original, recognition.read_bytes())
            self.assertEqual(2, distance("abc", "axy"))
            self.assertEqual("A!", normalized(" Ａ！\n"))


if __name__ == "__main__":
    unittest.main()
