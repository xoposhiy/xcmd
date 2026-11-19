# @implements CodeHealth, TraceStoryFiles
"""Compare two source snapshots using Roslyn metrics and direct story links."""

import argparse
from collections import defaultdict
import hashlib
import io
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import zipfile

from roslyn_metrics import collect_roslyn

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools/story-explorer"))
from catalogue import map_sources, parse_catalogue


PROJECTS = ("Xcmd.Core", "Xcmd.Infrastructure", "Xcmd.Desktop")
INPUTS = (
    "src", "tools", "config", "docs/stories", "global.json", "NuGet.Config",
    "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "CodeMetricsConfig.txt", ".editorconfig",
)
MEASUREMENT = {
    "sourceCharacters": "Count source characters with line endings normalized to LF and the byte order mark removed.",
    "complexity": "Sum Roslyn member cyclomatic complexity minus one, with a minimum of zero per member.",
    "weightedComplexity": "Multiply branching by the number of linked stories beyond the first.",
    "tangling": "Count distinct stories from direct @implements links.",
    "scattering": "Sum source characters across the distinct production C# files linked to a story.",
    "area": "Sum each interval width times its metric value. Divide by total width for the normalized area.",
}


def command(args, root, **options):
    return subprocess.run(args, cwd=root, check=True, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0), **options)


def git(root, *args):
    return command(["git", *args], root, stdout=subprocess.PIPE).stdout


def revision(root, ref):
    return git(root, "rev-parse", "--verify", "--end-of-options", ref + "^{commit}").decode("ascii").strip()


def capture(root, destination, commit):
    if commit:
        paths = [entry.decode("utf-8") for entry in
                 git(root, "ls-tree", "-z", "--name-only", commit, "--", *INPUTS).split(b"\0") if entry]
        archive = git(root, "archive", "--format=zip", commit, "--", *paths)
        with zipfile.ZipFile(io.BytesIO(archive)) as files:
            files.extractall(destination)
        return
    paths = git(root, "ls-files", "--cached", "--others", "--exclude-standard", "-z", "--", *INPUTS)
    for name in sorted(set(paths.decode("utf-8").split("\0")) - {""}):
        source = root / name
        if source.is_file() and not source.is_symlink() and source.resolve().is_relative_to(root):
            target = destination / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)


def measure(root, package_cache, runner, output, label):
    projects = [root / "src" / name / f"{name}.csproj" for name in PROJECTS]
    log = output / f"{label}-roslyn.log"
    xml = output / f"{label}-roslyn.xml"
    print(f"CHECK Measure the {label} snapshot", flush=True)
    with log.open("wb") as stream:
        try:
            command([
                "dotnet", "restore", str(projects[-1]), "-r", "win-x64", "-p:Configuration=Release",
                "--configfile", str(root / "NuGet.Config"), "--packages", str(package_cache),
                "--locked-mode", "--disable-parallel", "-m:1",
            ], root, stdout=stream, stderr=subprocess.STDOUT)
            command([str(runner), *(f"/project:{path}" for path in projects), f"/out:{xml}"],
                    root, stdout=stream, stderr=subprocess.STDOUT)
        except subprocess.CalledProcessError as error:
            raise ValueError(f"Could not measure the {label} snapshot. See {log}.") from error
    return xml


def read_text(path):
    return path.read_text(encoding="utf-8-sig")


def source_files(root):
    return sorted(path for project in PROJECTS for path in (root / "src" / project).rglob("*.cs")
                  if not {"bin", "obj"}.intersection(path.relative_to(root).parts))


def distribution(rows, metric, width):
    points, position = [], 0
    for name, row in sorted(rows.items(), key=lambda item: (-item[1][metric], item[0])):
        span = row[width] if width else 1
        points.append({"id": name, "start": position, "end": position + span,
                       "midpoint": position + span / 2, "value": row[metric]})
        position += span
    area = sum((point["end"] - point["start"]) * point["value"] for point in points)
    return {"width": position, "sum": sum(point["value"] for point in points),
            "max": max((point["value"] for point in points), default=0),
            "area": area, "normalizedArea": area / position if position else 0, "points": points}


def branching(member):
    return max(0, member.get("ccn", 0) - 1)


def weighted_complexity(complexity, stories):
    return complexity * max(0, len(stories) - 1)


def snapshot(root, metrics_path, description, commit):
    paths = source_files(root)
    documents = [(path.relative_to(root).as_posix(), read_text(path))
                 for path in sorted((root / "docs/stories").glob("*.md")) if path.name != "README.md"]
    sources = [(path.relative_to(root).as_posix(), read_text(path)) for path in paths]
    mapping = map_sources(parse_catalogue(documents), sources)
    if mapping["issues"]:
        issue = mapping["issues"][0]
        raise ValueError(f"{issue['path']}:{issue['line']}: {issue['message']}")

    projects, symbols, _ = collect_roslyn(root, metrics_path, paths)
    if {project["name"] for project in projects} != set(PROJECTS):
        raise ValueError("Roslyn metrics must include all three production projects.")
    complexity = defaultdict(int)
    for symbol in symbols:
        complexity[symbol["path"]] += branching(symbol)

    files = {}
    for name, text in sources:
        files[name] = {"sourceCharacters": len(text), "sourceSha256": hashlib.sha256(text.encode()).hexdigest(),
                       "complexity": complexity[name], "stories": []}
    stories = {}
    for epic in mapping["epics"]:
        for story in epic["stories"]:
            identifier = story["id"]
            linked_files = sorted({link["path"] for link in story["implements"]})
            for path in linked_files:
                files[path]["stories"].append(identifier)
            stories[identifier] = {"epic": epic["id"], "role": epic["role"], "files": linked_files,
                                   "fileCount": len(linked_files),
                                   "scattering": sum(files[path]["sourceCharacters"] for path in linked_files)}
    for row in files.values():
        row["stories"].sort()
        row["tangling"] = len(row["stories"])
        row["weightedComplexity"] = weighted_complexity(row["complexity"], row["stories"])

    digest = hashlib.sha256()
    for name, text in sorted(sources + documents):
        digest.update(f"{name}\0{text}\0".encode())
    distributions = {metric: distribution(files, metric, "sourceCharacters")
                     for metric in ("complexity", "weightedComplexity", "tangling")}
    distributions["scattering"] = distribution(stories, "scattering", None)
    return {
        "schemaVersion": 3, "snapshot": description, "commit": commit, "sourceSnapshotSha256": digest.hexdigest(),
        "scope": "Authored C# in the three production projects and all catalogue stories.",
        "roslynMetrics": "5.6.0", "measurement": MEASUREMENT,
        "files": files, "stories": stories,
        "metrics": {
            "fileCount": len(files), "storyCount": len(stories),
            "storiesWithProductionLinks": sum(bool(story["files"]) for story in stories.values()),
            "sourceCharacters": distributions["tangling"]["width"],
            "weightedComplexity": distributions["weightedComplexity"]["sum"],
            "complexity": distributions["complexity"]["sum"], "implementationLinks": distributions["tangling"]["sum"],
            "sharedLinkedSourceCharacters": distributions["scattering"]["sum"],
        },
        "distributions": distributions,
    }


def changed_items(before, after):
    return [name for name in sorted(before.keys() | after.keys()) if before.get(name) != after.get(name)]


def comparison(before, after, title):
    changed_files = changed_items(before["files"], after["files"])
    return {"title": title, "before": before, "after": after,
            "changedFiles": changed_files,
            "changedStories": changed_items(before["stories"], after["stories"])}


def render(output, report):
    for label in ("before", "after"):
        (output / f"{label}.json").write_text(json.dumps(report[label], ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    serialized = json.dumps(report, ensure_ascii=False, separators=(",", ":")).replace("<", "\\u003c")
    template = Path(__file__).with_name("refactoring.html").read_text(encoding="utf-8")
    (output / "index.html").write_text(template.replace("__REPORT_DATA__", serialized), encoding="utf-8")


def generate(root, runner, baseline, target, output, title):
    before_commit = revision(root, baseline)
    after_commit = revision(root, target) if target else None
    output.mkdir(parents=True, exist_ok=True)
    data = {}
    with tempfile.TemporaryDirectory(prefix="xcmd-comparison-") as folder:
        for label, commit in (("before", before_commit), ("after", after_commit)):
            capture(root, Path(folder) / label, commit)
        for label, commit in (("before", before_commit), ("after", after_commit)):
            source = Path(folder) / label
            metrics = measure(source, root / ".packages", runner, output, label)
            data[label] = snapshot(source, metrics, "Commit" if commit else "Working tree", commit)
    render(output, comparison(data["before"], data["after"], title))
    print(f"Refactoring comparison: {output / 'index.html'}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--runner", type=Path, required=True)
    parser.add_argument("--baseline", required=True)
    parser.add_argument("--target")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--title", required=True)
    args = parser.parse_args()
    try:
        generate(args.root.resolve(), args.runner.resolve(), args.baseline, args.target, args.output.resolve(), args.title)
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        parser.exit(1, f"{error}\n")


if __name__ == "__main__":
    main()
