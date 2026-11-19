# @tests HeadlessVerification, QualityChecks, CodeHealth
import json
import tempfile
import unittest
from pathlib import Path

from ci_summary import summarize


class VerificationSummaryTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.report = Path(directory.name)
        self.console = self.report / "console.log"

    def check_failed(self, log):
        (self.report / "verification.json").write_text('{"status":"failed"}', encoding="utf-8")
        (self.report / "verification.log").write_text(log, encoding="utf-8")
        return summarize(self.report, self.console, "failure", "https://example.org/artifact")

    def test_each_failed_scenario_appears_on_the_run_page(self):
        results = {"total": 3, "failures": 2, "scenarios": [
            {"Id": "one", "passed": False, "failure": "Expected <file>\n   at Framework.Dispatch()"},
            {"Id": "two", "passed": False, "failure": "Wrong count"},
            {"Id": "three", "passed": True}]}
        (self.report / "behavior").mkdir()
        (self.report / "behavior/results.json").write_text(json.dumps(results), encoding="utf-8")
        summary, annotations = self.check_failed("CHECK Behavioral scenarios\n1/3 passed. Results: results.json")
        self.assertIn("1/3", summary)
        self.assertIn("Expected &lt;file&gt;", summary)
        self.assertIn("https://example.org/artifact", summary)
        self.assertEqual(annotations, ["::error title=one::Expected <file>", "::error title=two::Wrong count"])

    def test_linter_failure_includes_its_name_and_diagnostics(self):
        summary, annotations = self.check_failed(
            "CHECK Build and run compiler analyzers\nBuild succeeded.\n"
            "CHECK C# whitespace formatting\nViewer.cs(7,1): error WHITESPACE\nExpected 50%.")
        self.assertIn("C# whitespace formatting", summary)
        self.assertIn("Viewer.cs(7,1)", annotations[0])
        self.assertIn("%0AExpected 50%25", annotations[0])

    def test_report_failure_before_results_uses_console_output(self):
        self.console.write_text("Could not restore reporting tools.", encoding="utf-8")
        summary, annotations = summarize(self.report, self.console, "failure", "")
        self.assertIn("restore reporting tools", summary)
        self.assertEqual(len(annotations), 1)
