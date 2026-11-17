# @implements IntegrationVerification
"""Isolated catalogue, worktree and HTTP fixtures for explorer scenarios."""

import subprocess
import tempfile
import threading
from contextlib import contextmanager
from pathlib import Path

from server import ExplorerServer


def epic_document(epic_id="ExampleEpic", order=1, story_ids=("ReadStory", "TraceFiles")):
    header = f'---\nid: {epic_id}\ntitle: "Example epic"\norder: {order}\nrole: developer\n---\n\n'
    body = "# Example epic\n\nAs a developer, I want to learn the product so that I can change it.\n\n[All epics](README.md)\n"
    for story_id in story_ids:
        body += f"\n## {story_id}\n\nAs a developer, I want to read {story_id} so that I can find related code.\n"
    return header + body


def write(root, path, text):
    target = root / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(text, encoding="utf-8")
    return target


@contextmanager
def worktree():
    with tempfile.TemporaryDirectory(prefix="xcmd-stories-") as folder:
        root = Path(folder).resolve()
        subprocess.run(["git", "init", "--quiet", str(root)], check=True, capture_output=True,
                       creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        write(root, "docs/stories/example.md", epic_document())
        write(root, ".gitignore", "ignored/\n")
        yield root


@contextmanager
def running_server(root):
    with ExplorerServer(root, 0) as server:
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            yield f"http://127.0.0.1:{server.server_port}"
        finally:
            server.shutdown()
            thread.join(timeout=5)
