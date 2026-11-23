"""Count labels and score predictions for individual recorded tool calls."""
import argparse
import hashlib
import json
import sys
import zipfile
from collections import Counter
from pathlib import Path
from statistics import mean

from trace import HERE, MANIFEST, load_trace, tool_calls

sys.path.insert(0, str(HERE.parent / "multilabel"))
from score import ACTIONS, COMMON_TARGETS, intersect_over_union


TARGETS = COMMON_TARGETS | {"project"}
ARCHIVE = HERE / "tool-calls.zip"


def record_ids(session, meta):
    return [f"{session}-L{n}" for n, _, _ in tool_calls(meta)]


def check_ids(actual, expected):
    missing = set(expected) - set(actual)
    extra = set(actual) - set(expected)
    if missing or extra:
        raise ValueError(f"Missing IDs: {sorted(missing)}. Unexpected IDs: {sorted(extra)}")


def has_text(value):
    return isinstance(value, str) and bool(value.strip())


def read_rows(data, expected, *, prediction=False, actions=ACTIONS, targets=TARGETS):
    rows = {}
    for line, raw in enumerate(data.decode("utf-8-sig").splitlines(), 1):
        try:
            row = json.loads(raw)
            key = row["id"]
            if not has_text(key) or key in rows:
                raise ValueError(f"Missing or duplicate ID: {key}")
            if not prediction and not has_text(row.get("summary")):
                raise ValueError(f"{key}: empty summary")
            pairs = row["pairs"]
            if not isinstance(pairs, list) or (not pairs and not prediction):
                raise ValueError(f"{key}: expected a list of pairs")
            seen = set()
            for entry in pairs:
                target, action = entry["target"], entry["action"]
                pair = (target, action)
                if target not in targets or action not in actions or pair in seen:
                    raise ValueError(f"{key}: invalid or duplicate pair: {pair}")
                seen.add(pair)
                if not prediction:
                    objects = entry.get("objects")
                    if not isinstance(objects, list) or not objects or not all(map(has_text, objects)):
                        raise ValueError(f"{key}: expected concrete objects for {pair}")
            rows[key] = row
        except (KeyError, TypeError, ValueError) as error:
            raise ValueError(f"Line {line}: {error}") from error
    check_ids(rows, expected)
    return rows


def pair_set(row):
    return {(p["target"], p["action"]) for p in row["pairs"]}


def compare(ids, a, b):
    pairs = [(pair_set(a[key]), pair_set(b[key])) for key in ids]
    return {
        "calls": len(ids),
        "exact": sum(left == right for left, right in pairs),
        "disjoint": sum(not (left & right) for left, right in pairs),
        "mean_jaccard": mean(intersect_over_union(left, right) for left, right in pairs),
    }


def read_archive(manifest, archive_path=ARCHIVE, reviewers=("a", "b"), actions=ACTIONS, targets=TARGETS):
    a, b = {}, {}
    with zipfile.ZipFile(archive_path) as archive:
        members = [f"{r}/{s}.jsonl" for r in reviewers for s in manifest]
        if sorted(archive.namelist()) != sorted(members):
            raise ValueError("Review archive members differ from the manifest")
        for s, meta in manifest.items():
            ids = record_ids(s, meta)
            for name, combined in zip(reviewers, (a, b), strict=True):
                member = f"{name}/{s}.jsonl"
                combined.update(read_rows(archive.read(member), ids, actions=actions, targets=targets))
    return a, b


def original_ids():
    context = HERE.parent / "context"
    inputs = [context / "study/observations.zip", context / "literal-study/reserve-observations.zip"]
    ids = []
    for path in inputs:
        with zipfile.ZipFile(path) as source:
            for member in source.namelist():
                ids.extend(row["id"] for row in json.loads(source.read(member))["rows"])
    return sorted(ids)


def agreement(manifest, a, b):
    groups = {s: record_ids(s, meta) for s, meta in manifest.items()}
    return {"total": compare(a.keys(), a, b),
            "sessions": {s: compare(ids, a, b) for s, ids in groups.items()}}


def label_counts(a, b):
    vocabulary = {"target": sorted(TARGETS), "action": sorted(ACTIONS),
                  "pair": [f"{t}/{v}" for t in sorted(TARGETS) for v in sorted(ACTIONS)]}
    counts = {group: {label: Counter(a=0, b=0, both=0) for label in labels}
              for group, labels in vocabulary.items()}
    for key in a:
        for group in vocabulary:
            left, right = (call_labels(reviewer[key], group) for reviewer in (a, b))
            for label in left | right:
                counts[group][label].update(a=int(label in left), b=int(label in right),
                                            both=int(label in left and label in right))
    return {"tool_calls": len(a), **counts}


def call_labels(row, group):
    if group == "pair":
        return {f"{target}/{action}" for target, action in pair_set(row)}
    return {p[group] for p in row["pairs"]}


def main():
    manifest = json.loads(MANIFEST.read_text(encoding="utf8"))
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    check = sub.add_parser("check", help="Check one complete reviewer file")
    check.add_argument("session", choices=manifest)
    check.add_argument("file", type=Path)
    verify = sub.add_parser("verify", help="Verify the published dataset and scores")
    verify.add_argument("--traces", type=Path, metavar="ROOT",
                        help="Also verify each original rollout and rebuild its inventory")
    sub.add_parser("counts", help="Count tool calls by target, action and target/action pair")
    score = sub.add_parser("score", help="Compare predictions to each independent reviewer")
    score.add_argument("session", choices=[*manifest, "all"])
    score.add_argument("view", choices=["item"], nargs="?", default="item", help=argparse.SUPPRESS)
    score.add_argument("file", type=Path)
    args = parser.parse_args()

    if args.command == "check":
        ids = record_ids(args.session, manifest[args.session])
        read_rows(args.file.read_bytes(), ids)
        print(f"Verified {len(ids)} records in {args.session}.")
        return
    a, b = read_archive(manifest)
    if args.command == "counts":
        print(json.dumps(label_counts(a, b), indent=2))
        return
    if args.command == "score":
        sessions = manifest if args.session == "all" else [args.session]
        ids = [key for s in sessions for key in record_ids(s, manifest[s])]
        predictions = read_rows(args.file.read_bytes(), ids, prediction=True)
        result = {name: compare(ids, reviewer, predictions)
                  for name, reviewer in [("against_a", a), ("against_b", b)]}
        print(json.dumps(result, indent=2))
        return

    legacy = sorted(f"{s}-L{n}" for s, meta in manifest.items() for n in meta["legacy"])
    if original_ids() != legacy or not set(legacy) <= a.keys():
        raise ValueError("Original sample IDs differ from the reviewed records")
    if args.traces:
        for meta in manifest.values():
            load_trace(meta, args.traces)
    scores = agreement(manifest, a, b)
    published = json.loads((HERE / "agreement.json").read_text(encoding="utf8"))
    if hashlib.sha256(ARCHIVE.read_bytes()).hexdigest() != published["tool_calls_sha256"]:
        raise ValueError("Review archive differs from the published dataset")
    if scores != published["scores"]["item"]:
        raise ValueError("Published agreement differs from the annotations")
    print(f"Verified both reviews for {len(a)} tool calls, including all {len(legacy)} original IDs.")
    print(json.dumps(scores["total"]))


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, zipfile.BadZipFile) as error:
        sys.exit(str(error))
