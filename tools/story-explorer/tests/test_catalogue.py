# @tests BrowseStories, TraceStoryFiles
import unittest

from catalogue import map_sources, parse_annotations, parse_catalogue
from fixtures import epic_document


class CatalogueScenarios(unittest.TestCase):
    def test_reading_order_comes_from_epic_order_and_story_position(self):
        epics = parse_catalogue([
            ("a.md", epic_document("LaterEpic", 2, ("LaterStory",))),
            ("z.md", epic_document("FirstEpic", 1, ("SecondName", "FirstName"))),
        ])
        self.assertEqual(["FirstEpic", "LaterEpic"], [epic["id"] for epic in epics])
        self.assertEqual(["SecondName", "FirstName"], [story["id"] for story in epics[0]["stories"]])

    def test_notes_and_code_examples_do_not_create_stories(self):
        document = epic_document(story_ids=("ReadStory",))
        document += "\n### Implementation decisions\n\nKeep examples.\n\n```md\n## ExampleOnly\n```\n"
        epic = parse_catalogue([("example.md", document)])[0]
        self.assertEqual(1, len(epic["stories"]))
        self.assertIn("## ExampleOnly", epic["stories"][0]["body"])
        self.assertNotIn("Implementation decisions", epic["stories"][0]["statement"])

    def test_invalid_catalogue_fails_with_context(self):
        invalid = [
            (epic_document().replace("order: 1", "order: zero"), "order"),
            (epic_document(story_ids=("SameId", "SameId")), "repeated ID"),
            (epic_document(story_ids=("ExampleEpic",)), "repeated ID"),
            (epic_document().replace("## ReadStory", "## invalid-id"), "invalid story ID"),
            (epic_document().replace("# Example epic", "# Other title"), "main heading"),
            (epic_document().replace("I want to read", "I can read"), "story statement"),
        ]
        for document, message in invalid:
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                parse_catalogue([("broken.md", document)])
        with self.assertRaisesRegex(ValueError, "repeated epic order"):
            parse_catalogue([("a.md", epic_document()), ("b.md", epic_document("AnotherEpic"))])

    def test_annotations_are_header_comments_in_each_supported_style(self):
        headers = ["// @implements ReadStory\n", "#!/usr/bin/python\n# @implements ReadStory\n",
                   '<?xml version="1.0"?>\n<!-- @implements ReadStory -->\n',
                   "<!doctype html>\n<!--\n @implements ReadStory\n-->\n",
                   "/*\n * @implements ReadStory\n */\n"]
        for header in headers:
            with self.subTest(header=header):
                relations, issues = parse_annotations(header + 'code\n// @tests TraceFiles\n', "source")
                self.assertEqual(["ReadStory"], [item["id"] for item in relations])
                self.assertEqual([], issues)

    def test_relations_are_distinct_and_unknown_ids_are_reported(self):
        result = map_sources(parse_catalogue([("epic.md", epic_document())]), [
            ("shared.cs", "// @implements ReadStory, ReadStory, TraceFiles\n// @tests ReadStory\nclass Test {}"),
            ("wrong.py", "# @implements RetiredStory\n# @tests ReadStory,\n"),
        ])
        story = result["epics"][0]["stories"][0]
        self.assertEqual([{"path": "shared.cs", "line": 1}], story["implements"])
        self.assertEqual([{"path": "shared.cs", "line": 2}], story["tests"])
        self.assertEqual(2, len(result["issues"]))
        self.assertTrue(any("RetiredStory" in issue["message"] for issue in result["issues"]))


if __name__ == "__main__":
    unittest.main()
