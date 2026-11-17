# @tests BrowseStories, TraceStoryFiles
import json
import unittest
from urllib.error import HTTPError
from urllib.request import Request, urlopen

from fixtures import running_server, worktree, write
from repository import read_snapshot


class RepositoryScenarios(unittest.TestCase):
    def test_current_sources_include_new_files_and_exclude_generated_and_archived_files(self):
        with worktree() as root:
            for path in ("src/new.cs", "tests/check.py", "ignored/old.cs", "src/bin/generated.cs",
                         "docs/archive/old.cs", "docs/codex-sessions/session.py"):
                write(root, path, "// @implements ReadStory\n")
            result = read_snapshot(root)
            self.assertEqual(["src/new.cs", "tests/check.py"], result["files"])
            write(root, "src/new.cs", "// @implements TraceFiles\n")
            refreshed = read_snapshot(root)
            self.assertEqual(["tests/check.py"], [item["path"] for item in refreshed["epics"][0]["stories"][0]["implements"]])

    def test_http_serves_current_map_and_only_known_source_as_plain_text(self):
        with worktree() as root:
            write(root, "src/view.html", "<!-- @implements ReadStory -->\n<script>window.bad = true;</script>")
            write(root, "private.txt", "not mapped")
            with running_server(root) as url:
                with urlopen(url + "/") as response:
                    self.assertIn(b"Story explorer", response.read())
                with urlopen(url + "/source?path=src/view.html") as response:
                    self.assertEqual("text/plain", response.headers.get_content_type())
                    self.assertEqual("nosniff", response.headers["X-Content-Type-Options"])
                for path in ("/source?path=private.txt", "/source?path=../AGENTS.md", "/unknown"):
                    with self.subTest(path=path), self.assertRaises(HTTPError) as error:
                        urlopen(url + path)
                    self.assertEqual(404, error.exception.code)
                write(root, "src/new.cs", "// @tests TraceFiles\n")
                with urlopen(url + "/api/catalogue") as response:
                    result = json.load(response)
                self.assertEqual([{"path": "src/new.cs", "line": 1}], result["epics"][0]["stories"][1]["tests"])
                with self.assertRaises(HTTPError) as error:
                    urlopen(Request(url + "/api/catalogue", headers={"Host": "untrusted.example"}))
                self.assertEqual(403, error.exception.code)

    def test_invalid_reload_returns_visible_error_instead_of_partial_catalogue(self):
        with worktree() as root, running_server(root) as url:
            write(root, "docs/stories/example.md", "broken")
            with self.assertRaises(HTTPError) as error:
                urlopen(url + "/api/catalogue")
            self.assertEqual(422, error.exception.code)
            self.assertIn("front matter", json.load(error.exception)["error"])


if __name__ == "__main__":
    unittest.main()
