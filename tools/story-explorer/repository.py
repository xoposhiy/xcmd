# @implements BrowseStories, TraceStoryFiles
"""Read the current worktree, excluding ignored and generated files."""

import subprocess
from datetime import datetime, timezone
from pathlib import Path

from catalogue import map_sources, parse_catalogue
from dependencies import dependency_groups


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


def is_matrix_source(path):
    if not path.startswith("src/"):
        return False
    source = Path(path)
    if source.suffix.lower() != ".txt":
        return True
    name = source.stem.lower()
    return name != "license" and not name.startswith("license-") and not name.endswith("-license")


def read_snapshot(root):
    root = root.resolve()
    documents = [(path.relative_to(root).as_posix(), path.read_text(encoding="utf-8-sig"))
                 for path in sorted((root / "docs/stories").glob("*.md")) if path.name != "README.md"]
    epics = parse_catalogue(documents)
    paths = list(source_paths(root))
    production = [path for path in paths if is_matrix_source(path)]
    sizes = {}
    dependency_sources = {}

    def sources():
        for path in paths:
            raw = (root / path).read_bytes()
            sizes[path] = len(raw)
            source = raw.decode("utf-8-sig")
            if path.startswith("src/") and path.lower().endswith((".cs", ".csproj")):
                dependency_sources[path] = source
            yield path, source

    snapshot = map_sources(epics, sources())
    snapshot["fileSizes"] = {path: sizes[path] for path in snapshot["files"]}
    snapshot["matrixFiles"] = production
    snapshot["matrixFileSizes"] = {path: sizes[path] for path in production}
    snapshot["dependencyGroups"] = dependency_groups(production, dependency_sources)
    snapshot["scannedFiles"] = len(paths)
    snapshot["generatedAt"] = datetime.now(timezone.utc).isoformat()
    return snapshot
