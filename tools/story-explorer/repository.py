# @implements BrowseStories, TraceStoryFiles
"""Read the current worktree, excluding ignored and generated files."""

import subprocess
from datetime import datetime, timezone
from pathlib import Path

from catalogue import map_sources, parse_catalogue


SOURCE_SUFFIXES = {
    ".cs", ".py", ".js", ".mjs", ".cjs", ".ts", ".tsx", ".jsx", ".css",
    ".ps1", ".sh", ".html", ".axaml", ".svg", ".xml", ".csproj", ".props",
    ".targets", ".slnx", ".config", ".yml", ".yaml", ".txt",
}
SOURCE_NAMES = {".gitignore", ".gitattributes", ".editorconfig"}
EXCLUDED_PARTS = {"bin", "obj", "artifacts", "releases", "node_modules", "__pycache__", ".git"}


def source_paths(root):
    result = subprocess.run(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"],
        cwd=root, check=True, capture_output=True,
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
    )
    for name in sorted(set(result.stdout.decode("utf-8").split("\0")) - {""}):
        path = Path(name)
        if path.parts[0] == "docs" or EXCLUDED_PARTS.intersection(path.parts):
            continue
        if path.suffix.lower() not in SOURCE_SUFFIXES and path.name not in SOURCE_NAMES:
            continue
        candidate = root / path
        if candidate.is_file() and not candidate.is_symlink() and candidate.resolve().is_relative_to(root):
            yield name


def read_snapshot(root):
    root = root.resolve()
    documents = [(path.relative_to(root).as_posix(), path.read_text(encoding="utf-8-sig"))
                 for path in sorted((root / "docs/stories").glob("*.md")) if path.name != "README.md"]
    epics = parse_catalogue(documents)
    paths = list(source_paths(root))
    sources = ((path, (root / path).read_text(encoding="utf-8-sig")) for path in paths)
    snapshot = map_sources(epics, sources)
    snapshot["scannedFiles"] = len(paths)
    snapshot["generatedAt"] = datetime.now(timezone.utc).isoformat()
    return snapshot
