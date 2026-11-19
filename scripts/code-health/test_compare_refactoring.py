# @tests CodeHealth, TraceStoryFiles
"""Check file-based comparison metrics and report output."""

from pathlib import Path
import tempfile
import unittest

from compare_refactoring import branching, changed_items, comparison, distribution, render, weighted_complexity


class ComparisonTests(unittest.TestCase):
    def test_weighted_complexity_charges_for_each_additional_story(self):
        complexity = sum(branching({"ccn": value}) for value in (0, 1, 5))
        self.assertEqual(4, complexity)
        self.assertEqual(0, weighted_complexity(complexity, []))
        self.assertEqual(0, weighted_complexity(complexity, ["NavigatePanels"]))
        self.assertEqual(8, weighted_complexity(complexity, ["NavigatePanels", "SortEntries", "RefreshEntries"]))

    def test_partial_files_keep_separate_points(self):
        files = {
            "PanelView.cs": {"sourceCharacters": 12, "weightedComplexity": 8},
            "PanelView.Search.cs": {"sourceCharacters": 30, "weightedComplexity": 4},
            "PanelView.History.cs": {"sourceCharacters": 8, "weightedComplexity": 4},
        }
        result = distribution(files, "weightedComplexity", "sourceCharacters")
        self.assertEqual(["PanelView.cs", "PanelView.History.cs", "PanelView.Search.cs"],
                         [point["id"] for point in result["points"]])
        self.assertEqual(50, result["width"])
        self.assertEqual(248, result["area"])
        self.assertEqual(248 / 50, result["normalizedArea"])
        self.assertEqual(16, result["sum"])

    def test_changed_files_include_additions_deletions_and_content_changes(self):
        before = {"changed.cs": {"hash": "a"}, "removed.cs": {}, "same.cs": {}}
        after = {"added.cs": {}, "changed.cs": {"hash": "b"}, "same.cs": {}}
        self.assertEqual(["added.cs", "changed.cs", "removed.cs"], changed_items(before, after))

    def test_report_embeds_escaped_data_and_keeps_downloads(self):
        before = {"files": {"old.cs": {}}, "stories": {}}
        after = {"files": {"new.cs": {}}, "stories": {}}
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder)
            render(output, comparison(before, after, "</script>"))
            html = (output / "index.html").read_text(encoding="utf-8")
            self.assertIn(r'"title":"\u003c/script>"', html)
            self.assertIn('"changedFiles":["new.cs","old.cs"]', html)
            self.assertIn('href="before.json"', html)
            self.assertIn('href="after.json"', html)
            self.assertNotIn("__REPORT_DATA__", html)


if __name__ == "__main__":
    unittest.main()
