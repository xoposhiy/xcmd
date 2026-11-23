"""Validate cached Luna responses for a complete session and score labelled calls."""

import argparse
import json
from collections import Counter
from statistics import mean
from zipfile import ZipFile

from session_capacity import OUT, locate, project_trace
from study import ACTIONS, REVIEWERS, REVISED, TARGETS, call_labels, read_rows


def parse_rows(reply):
    if reply["status"] != "completed":
        raise ValueError(f"Incomplete response: {reply['incomplete']}")
    rows = json.loads(reply["output"])["rows"]
    if not isinstance(rows, list):
        raise TypeError("Expected a rows array")
    return rows


def normalize(row):
    key = row["id"]
    if type(key) is not int:
        raise TypeError("Expected a numeric call ID")
    grouped = {}
    for pair in row["pairs"]:
        label = pair["target"], pair["action"]
        objects = pair["objects"]
        if (
            not isinstance(objects, list)
            or not objects
            or not all(isinstance(o, str) and o.strip() for o in objects)
        ):
            raise ValueError("Expected a list of concrete objects")
        if label not in grouped:
            grouped[label] = {**pair, "objects": []}
        target = grouped[label]["objects"]
        target.extend(obj for obj in objects if obj not in target)
    merged = dict(row, id=str(key), pairs=list(grouped.values()))
    read_rows(json.dumps(merged).encode(), [str(key)], actions=ACTIONS, targets=TARGETS)
    return key, merged, len(grouped) != len(row["pairs"])


def jaccard(expected, actual):
    scores = []
    for key, reference in expected.items():
        pairs = call_labels(reference, "pair")
        predicted = call_labels(actual[key], "pair") if key in actual else set()
        scores.append(len(pairs & predicted) / len(pairs | predicted))
    return mean(scores) if scores else None


def score(session, pattern, repair=()):
    path, project = locate(session)
    trace, _ = project_trace(path, project, include_compaction=True)
    markers = {row["id"] for row in trace if row.get("tool") == "ContextCompaction"}
    ids = {row["id"] for row in trace if "id" in row} - markers
    inventory = set()
    observed = set()
    result = {}
    extra, duplicate, invalid, normalized = [], [], [], []
    usage = Counter()
    seconds = []
    files = sorted(OUT.glob(pattern + "-*.json"))
    for sub in repair:
        files.extend(sorted(OUT.glob(sub + "-*.json")))
    if not files:
        raise ValueError(f"No responses for {pattern}")
    for file in files:
        reply = json.loads(file.read_text(encoding="utf8"))
        local = set(reply["expected"])
        inventory.update(local)
        usage.update({k: v for k, v in reply["usage"].items() if isinstance(v, int)})
        usage.update(reply["usage"]["output_tokens_details"])
        usage.update({k: v for k, v in reply["usage"]["input_tokens_details"].items() if isinstance(v, int)})
        seconds.append(reply["seconds"])
        try:
            rows = parse_rows(reply)
        except (KeyError, TypeError, ValueError) as error:
            invalid.append([file.name, str(error)])
            continue
        frequencies = Counter(
            row.get("id") for row in rows if isinstance(row, dict) and type(row.get("id")) is int
        )
        observed.update(frequencies.keys() & local)
        duplicate.extend(key for key, count in frequencies.items() if count > 1 and key not in markers)
        for row in rows:
            if isinstance(row, dict) and row.get("id") in markers:
                continue
            try:
                key, item, merged = normalize(row)
            except (KeyError, TypeError, ValueError) as error:
                invalid.append([file.name, str(error)])
                continue
            if key not in local or key not in ids:
                extra.append(key)
            elif frequencies[key] == 1:
                result[key] = item
                if merged:
                    normalized.append(key)
    if inventory - markers != ids:
        raise ValueError(f"Requests cover {len(inventory & ids)} of {len(ids)} original calls")
    counts = Counter(row["summary"] for row in result.values())
    stats = {
        "requests": len(files),
        "input_context_markers": sorted(inventory & markers),
        "calls": len(ids),
        "valid": len(result),
        "missing_ids": sorted(ids - observed),
        "unresolved_ids": sorted(ids - result.keys()),
        "unexpected_ids": extra,
        "duplicate_ids": duplicate,
        "merged_repeated_pairs": normalized,
        "errors": invalid,
        "usage": dict(usage),
        "summed_request_seconds": round(sum(seconds), 1),
        "max_identical_summary_count": max(counts.values(), default=0),
    }
    if session == "mdview":
        with ZipFile(REVISED / "tool-calls.zip") as labels:
            scores = {}
            for side in REVIEWERS:
                rows = map(json.loads, labels.read(side + "/v2.jsonl").splitlines())
                expected = {int(r["id"].split("-L")[1]): r for r in rows}
                if set(expected) != ids:
                    raise ValueError("Reference call IDs differ from the trace inventory")
                scores[side] = jaccard(expected, result)
            stats["mean_jaccard_against"] = scores
    return stats, result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("session")
    parser.add_argument("response_prefix")
    parser.add_argument("--repair", nargs="+", default=[])
    args = parser.parse_args()
    print(json.dumps(score(args.session, args.response_prefix, args.repair)[0], indent=2))
