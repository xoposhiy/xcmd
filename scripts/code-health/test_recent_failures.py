# @tests QualityChecks, HeadlessVerification
import json
import tempfile
import unittest
from collections import Counter
from pathlib import Path
from unittest.mock import patch

import recent_failures


class RecentFailureTests(unittest.TestCase):
    def test_summary_confirms_all_failed_ids_are_present(self):
        line = "Full verification\tTests\t2026-11-19T00:00:00.123Z "
        log = line + "FAIL copy-one (15 ms): Expected a file.\n"
        self.assertEqual(["copy-one"], recent_failures.failed_ids(log + line + "3/4 passed. Results: path"))
        self.assertIsNone(recent_failures.failed_ids(log + line + "2/4 passed. Results: path"))
        self.assertEqual([], recent_failures.failed_ids(line + "4/4 passed. Results: path"))

    def test_older_run_uses_its_behavior_report(self):
        def old_run(*args, **kwargs):
            if "view" in args:
                return "2026-11-19T00:00:00.123Z 1/2 passed. Results: path"
            report = Path(args[-1]) / "behavior"
            report.mkdir()
            (report / "results.json").write_text(json.dumps({"scenarios": [
                {"Id": "copy", "passed": False}, {"Id": "rename", "passed": True}]}))

        with patch.object(recent_failures, "command", side_effect=old_run):
            self.assertEqual(["copy"], recent_failures.read_attempt({"databaseId": 4, "attempt": 1}, 1))

    def test_window_counts_failures_once_per_run_and_reuses_cached_attempts_offline(self):
        runs = [{"databaseId": 1, "attempt": 1, "conclusion": "failure"},
                {"databaseId": 2, "attempt": 1, "conclusion": "success"},
                {"databaseId": 3, "attempt": 1, "conclusion": "failure"}]
        with tempfile.TemporaryDirectory() as directory:
            cache = Path(directory)
            with patch.object(recent_failures, "command", return_value=json.dumps(runs)), \
                    patch.object(recent_failures, "read_attempt", side_effect=[["copy"], ["copy", "rename"]]):
                self.assertEqual(Counter(copy=2, rename=1), recent_failures.history(cache))
            with patch.object(recent_failures, "command", side_effect=OSError("offline")):
                self.assertEqual(Counter(copy=2, rename=1), recent_failures.history(cache))
            runs[2]["attempt"] = 2
            with patch.object(recent_failures, "command", return_value=json.dumps(runs[1:])), \
                    patch.object(recent_failures, "read_attempt", return_value=["copy", "move"]) as read:
                self.assertEqual(Counter(copy=1, rename=1, move=1), recent_failures.history(cache))
                read.assert_called_once_with(runs[2], 2)
            runs[2].update(attempt=3, conclusion="success")
            with patch.object(recent_failures, "command", return_value=json.dumps(runs[1:])):
                self.assertEqual(Counter(copy=1, rename=1, move=1), recent_failures.history(cache))
