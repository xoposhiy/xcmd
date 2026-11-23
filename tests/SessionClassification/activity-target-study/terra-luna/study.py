"""Validate the revised annotations, score predictions and compare Terra with Luna."""
import argparse
import hashlib
import json
import sys
from collections import Counter
from pathlib import Path


HERE = Path(__file__).resolve().parent
BASE = HERE.parent / "terra"
sys.path.insert(0, str(BASE))
from confusion import UNPAIRED, confusion
from review import MANIFEST, agreement, call_labels, compare, read_archive, read_rows, record_ids


ACTIONS = ("read", "write", "exec", "other")
PREVIOUS_TARGETS = ("tooling", "docs", "unknown", "source", "tests", "git", "project", "tracker")
TARGETS = ("build_release", "agent_instructions", "tests", "source", "git", "docs", "tracker", "project", "unknown")
REVIEWERS = ("terra", "luna")
GUIDE = BASE / "proposed-guide.md"
SOFTWARE = HERE / "software"
REVISED = SOFTWARE / "revised"


def target_vocabulary(directory):
    return TARGETS if directory == REVISED else PREVIOUS_TARGETS


def report(manifest, directory):
    archive = directory / "tool-calls.zip"
    targets = target_vocabulary(directory)
    terra, luna = read_archive(manifest, archive, REVIEWERS, ACTIONS, targets)
    matrices = {}
    for axis, vocabulary in [("target", targets), ("action", ACTIONS)]:
        counts, cases = confusion(terra, luna, axis)
        labels = [*vocabulary, UNPAIRED]
        matrices[axis] = {
            "labels": labels,
            "rows_terra_columns_luna": [[counts[row, col] for col in labels] for row in labels],
            "calls_by_set_relation": dict(cases),
        }
    guide = directory / "guide.md" if directory == REVISED else GUIDE
    result = {
        "guide_sha256": hashlib.sha256(guide.read_text(encoding="utf8").encode()).hexdigest(),
        "archive_sha256": hashlib.sha256(archive.read_bytes()).hexdigest(),
        "agreement": agreement(manifest, terra, luna),
        "confusion": matrices,
    }
    if directory == SOFTWARE:
        note = (directory / "web-reads.md").read_text(encoding="utf8")
        result["web_read_guide_sha256"] = hashlib.sha256(note.encode()).hexdigest()
    if directory in (SOFTWARE, REVISED):
        result["manifest_sha256"] = hashlib.sha256((SOFTWARE / "manifest.json").read_text(encoding="utf8").encode()).hexdigest()
    if directory == REVISED:
        first_pass = directory / "first-pass.zip"
        initial = read_archive(manifest, first_pass, REVIEWERS, ACTIONS, targets)
        result["first_pass"] = {
            "archive_sha256": hashlib.sha256(first_pass.read_bytes()).hexdigest(),
            "agreement": agreement(manifest, *initial),
        }
        counts = {target: dict(terra=0, luna=0, both=0) for target in targets}
        for key in terra:
            left, right = (call_labels(rows[key], "target") for rows in (terra, luna))
            for target in left | right:
                counts[target]["terra"] += target in left
                counts[target]["luna"] += target in right
                counts[target]["both"] += target in left & right
        result["calls_by_target"] = counts
    return result


def tooling_groups(manifest, directory, examples):
    audit = json.loads((HERE / "tooling-groups.json").read_text(encoding="utf8"))
    archive = directory / "tool-calls.zip"
    digest = "source_archive_sha256" if directory == HERE else "software_archive_sha256"
    if hashlib.sha256(archive.read_bytes()).hexdigest() != audit[digest]:
        raise ValueError("Tooling themes were assigned to a different review archive")
    terra, luna = read_archive(manifest, archive, REVIEWERS, ACTIONS)
    groups = {theme: dict(terra=0, luna=0) for theme in audit["themes"]}
    details = []
    for side, rows in zip(REVIEWERS, (terra, luna)):
        tagged = {key: [p for p in row["pairs"] if p["target"] == "tooling"] for key, row in rows.items()}
        assigned = Counter()
        for session in manifest:
            for theme, lines in audit["assignments"][session][side].items():
                groups[theme][side] += len(lines)
                for n in lines:
                    key = f"{session}-L{n}"
                    assigned[key] += 1
                    if examples:
                        details.append(dict(id=key, reviewer=side, theme=theme,
                                            summary=rows[key]["summary"], tooling=tagged[key]))
        if assigned != Counter(k for k, pairs in tagged.items() if pairs):
            raise ValueError(f"Tooling themes do not cover every {side} annotation exactly once")
    return sorted(details, key=lambda r: r["theme"]) if examples else groups


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    version = parser.add_mutually_exclusive_group()
    version.add_argument("--original", action="store_true", help="Use the original eight-session comparison")
    version.add_argument("--previous", action="store_true", help="Use the earlier six-session reviews with a tooling target")
    commands = parser.add_subparsers(dest="command", required=True)
    check = commands.add_parser("check", help="Check one review against the complete item inventory")
    check.add_argument("session")
    check.add_argument("file", type=Path)
    commands.add_parser("compare", help="Compute pair agreement and both confusion matrices")
    commands.add_parser("verify", help="Reproduce the saved results")
    tooling = commands.add_parser("tooling", help="Count annotations by the main object labelled tooling")
    tooling.add_argument("--examples", action="store_true", help="Show the original summaries and tooling objects")
    score = commands.add_parser("score", help="Score predictions against each revised review")
    score.add_argument("session")
    score.add_argument("file", type=Path)
    args = parser.parse_args()
    directory = REVISED
    if args.original:
        directory = HERE
    elif args.previous or args.command == "tooling":
        directory = SOFTWARE
    targets = target_vocabulary(directory)
    manifest_file = MANIFEST if args.original else SOFTWARE / "manifest.json"
    manifest = json.loads(manifest_file.read_text(encoding="utf8"))
    if args.command in ("check", "score") and args.session not in manifest:
        if args.command != "score" or args.session != "all":
            parser.error(f"Unknown session: {args.session}")
    if args.command == "check":
        ids = record_ids(args.session, manifest[args.session])
        read_rows(args.file.read_bytes(), ids, actions=ACTIONS, targets=targets)
        print(f"Verified {len(ids)} tool calls in {args.session}.")
    elif args.command == "tooling":
        print(json.dumps(tooling_groups(manifest, directory, args.examples), indent=2))
    elif args.command == "score":
        sessions = manifest if args.session == "all" else [args.session]
        ids = [key for session in sessions for key in record_ids(session, manifest[session])]
        predictions = read_rows(args.file.read_bytes(), ids, prediction=True, actions=ACTIONS, targets=targets)
        reviews = read_archive(manifest, directory / "tool-calls.zip", REVIEWERS, ACTIONS, targets)
        print(json.dumps({name: compare(ids, rows, predictions)
                          for name, rows in zip(REVIEWERS, reviews)}, indent=2))
    else:
        result = report(manifest, directory)
        if args.command == "verify":
            saved = json.loads((directory / "results.json").read_text(encoding="utf8"))
            if result != saved["results"]:
                raise ValueError("Published results differ from this run")
            print("Verified both complete reviews, agreement and confusion matrices.")
        else:
            print(json.dumps(result, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError) as error:
        sys.exit(str(error))
