"""Validate resource labels, build the working reference and measure set agreement."""
import argparse
import json
import posixpath
import re
from collections import Counter
from itertools import combinations
from pathlib import Path
from urllib.parse import unquote, urlsplit, urlunsplit

from annotate import HERE, KINDS, MANIFEST, MODELS, GUIDE, batches, digest, encode, evidence, tool_calls, write

NAMES = list(MODELS)
PAIRS = list(combinations(NAMES, 2))


def read_json(path):
    return json.loads(path.read_text(encoding="utf8"))


def read_rows(path):
    rows = [json.loads(line) for line in path.read_text(encoding="utf8").splitlines()]
    result = {r["id"]: r for r in rows}
    if len(result) != len(rows):
        raise ValueError(f"Duplicate call in {path}")
    return result


def canonical(kind, value):
    value = value.strip()
    if kind in ("file", "directory"):
        if value.lower().startswith("file:///"):
            value = unquote(value[8:])
        value = posixpath.normpath(value.replace("\\", "/"))
    elif kind == "url":
        url = urlsplit(value)
        if url.scheme in ("http", "https") and url.hostname:
            # Hosts are case-insensitive. Paths, queries and fragments retain case.
            value = urlunsplit((url.scheme, url.netloc.lower(), url.path or "/", url.query, url.fragment))
    if kind not in KINDS or not value:
        raise ValueError(f"Invalid resource: {kind}, {value!r}")
    return kind, value


def resource_key(pair):
    kind, value = pair
    return kind, value.casefold() if kind in ("file", "directory") else value


def unpack(resources, spellings):
    result = set()
    for r in resources:
        pair = canonical(r["kind"], r["value"])
        key = resource_key(pair)
        spellings.setdefault(key, pair[1])
        result.add(key)
    return result


def packed(values, spellings):
    return [{"kind": kind, "value": spellings[kind, value]} for kind, value in sorted(values)]


def trace_reference():
    labels = {}
    # A complete call consisting only of literal Get-Content operands and output slicing.
    operand = r"""(?:'[^'*?$]+'|"[^"*?$]+"|[\w./\\~-]+)"""
    read = re.compile(rf"Get-Content\s+(?:(?:-Raw|-LiteralPath|-Path|-Encoding\s+UTF8)\s+)*"
                      rf"(?P<files>{operand}(?:\s*,\s*{operand})*)(?:\s+-(?:TotalCount|Tail)\s+\d+)?"
                      rf"(?:\s*\|\s*Select-Object(?:\s+-(?:Skip|First|Last)\s+\d+)*)?", re.I)
    for _, _, rows in evidence():
        for row in rows:
            paths = []
            if row["tool"] == "FileChange" and row["status"] == "completed":
                source = "patch"
                paths = [p for old, _, new in row["changes"] for p in (old, new) if p]
            elif row["tool"] == "ImageView":
                source = "image"
                paths = [row["path"]]
            elif row.get("exit") == 0 and row.get("cwd") == ".":
                source = "literal read"
                for part in row["command"].split(";"):
                    match = read.fullmatch(part.strip())
                    if not match:
                        paths = []
                        break
                    paths.extend(p.strip("\"'") for p in re.findall(operand, match["files"]))
            if paths:
                spellings = {}
                keys = unpack([{"kind": "file", "value": p} for p in paths], spellings)
                labels[row["id"]] = {"id": row["id"], "source": source, "resources": packed(keys, spellings)}
    return labels


def fraction(n, d):
    return round(n / d, 6) if d else None


def agreement(groups, names=NAMES):
    total = exact = nonempty = same_nonempty = intersection = union = 0
    overlap_sum = 0.0
    votes = Counter()
    for group in groups:
        sets = [group[name] for name in names]
        shared = set.intersection(*sets)
        observed = set.union(*sets)
        total += 1
        exact += shared == observed
        if observed:
            nonempty += 1
            same_nonempty += shared == observed
            overlap_sum += len(shared) / len(observed)
            intersection += len(shared)
            union += len(observed)
            votes.update(Counter(x for s in sets for x in s).values())
    return {"calls": total, "exact": exact, "active": nonempty, "exact_active": same_nonempty,
            "jaccard_active": fraction(overlap_sum, nonempty), "shared": intersection, "union": union,
            "pooled_jaccard": fraction(intersection, union), "votes": dict(sorted(votes.items()))}


def accuracy(calls, refs, name):
    tp = fp = fn = exact = 0
    for i, truth in refs.items():
        predicted = calls[i][name]
        tp += len(predicted & truth)
        fp += len(predicted - truth)
        fn += len(truth - predicted)
        exact += predicted == truth
    return {"calls": len(refs), "exact": exact, "tp": tp, "fp": fp, "fn": fn,
            "precision": fraction(tp, tp + fp), "recall": fraction(tp, tp + fn), "f1": fraction(2 * tp, 2 * tp + fp + fn)}


def kind_matrix(calls, left, right):
    kinds = [*KINDS, "absent"]
    matrix = {a: dict.fromkeys(kinds, 0) for a in kinds}
    for group in calls.values():
        a, b = group[left], group[right]
        for kind, _ in a & b:
            matrix[kind][kind] += 1
        left_only, right_only = a - b, b - a
        for value in {v for _, v in left_only} & {v for _, v in right_only}:
            ls = {x for x in left_only if x[1] == value}
            rs = {x for x in right_only if x[1] == value}
            if len(ls) == len(rs) == 1:
                matrix[next(iter(ls))[0]][next(iter(rs))[0]] += 1
                left_only -= ls
                right_only -= rs
        for kind, _ in left_only:
            matrix[kind]["absent"] += 1
        for kind, _ in right_only:
            matrix["absent"][kind] += 1
    matrix["absent"]["absent"] = None
    return matrix


def calculate(folder, compare_traces=False):
    manifest = read_json(MANIFEST)
    ids = {f"{trace}-L{line}": trace for trace, meta in manifest.items() for line, _, _ in tool_calls(meta)}
    calls = {i: {} for i in ids}
    spellings = {i: {} for i in ids}
    receipt = read_json(folder / "receipt.json")
    if receipt["guide_sha256"] != digest(GUIDE) or receipt["manifest_sha256"] != digest(MANIFEST.read_text(encoding="utf8")):
        raise ValueError("Guide or manifest digest differs")
    packet_hashes = {}
    for name in NAMES:
        rows = read_rows(folder / f"{name}.jsonl")
        requests = [r for r in receipt["requests"] if r["reviewer"] == name]
        recorded = [i for r in requests for i in r["ids"]]
        if rows.keys() != ids.keys() or sorted(recorded) != sorted(ids):
            raise ValueError(f"Call inventory differs for {name}")
        for r in requests:
            if (r["model"], r["effort"]) != MODELS[name]:
                raise ValueError(f"Model or effort differs for {name}")
            for i in r["ids"]:
                if packet_hashes.setdefault(i, r["input_sha256"]) != r["input_sha256"]:
                    raise ValueError(f"Reviewers saw different inputs for {i}")
            if r["labels_sha256"] != digest(encode([rows[i] for i in r["ids"]])):
                raise ValueError(f"Raw labels differ for {name} {r['ids'][0]}")
        for i, row in rows.items():
            calls[i][name] = unpack(row["resources"], spellings[i])
    anchors = read_rows(folder / "trace-reference.jsonl")
    if compare_traces:
        if trace_reference() != anchors:
            raise ValueError("Reference differs from the original traces")
        for _, project, rows in evidence():
            for packet in batches(rows):
                expected = digest(encode({"project": project, "calls": packet}))
                if any(packet_hashes[r["id"]] != expected for r in packet):
                    raise ValueError("Inference evidence differs from the original traces")
    refs = {i: unpack(r["resources"], spellings[i]) for i, r in anchors.items()}
    decisions = read_rows(folder / "review.jsonl")
    merged = []
    for i, group in calls.items():
        votes = Counter(x for s in group.values() for x in s)
        majority = {x for x, count in votes.items() if count >= 2}
        if i in refs:
            selected, source = refs[i], anchors[i]["source"]
        elif i in decisions:
            row = decisions[i]
            add = unpack(row["add"], spellings[i])
            drop = unpack(row["drop"], spellings[i])
            if (add & majority) or not drop <= majority:
                raise ValueError(f"Invalid review changes for {i}")
            selected, source = (majority - drop) | add, "trace review"
        elif all(group[n] == majority for n in NAMES):
            selected, source = majority, "unanimous"
        else:
            selected, source = majority, "majority"
        merged.append({"id": i, "resources": packed(selected, spellings[i]), "source": source})
    if decisions.keys() - ids.keys() or anchors.keys() - ids.keys() or decisions.keys() & anchors.keys():
        raise ValueError("Invalid reference IDs")
    pair_stats = {}
    for left, right in PAIRS:
        stat = agreement(calls.values(), (left, right))
        stat["left_only"] = sum(len(g[left] - g[right]) for g in calls.values())
        stat["right_only"] = sum(len(g[right] - g[left]) for g in calls.values())
        pair_stats[f"{left}/{right}"] = stat
    by_kind = {}
    for kind in [*KINDS, "non-file/non-URL"]:
        groups = [{n: {x for x in s if (x[0] not in ("file", "url") if kind == "non-file/non-URL" else x[0] == kind)}
                   for n, s in g.items()} for g in calls.values()]
        by_kind[kind] = agreement(groups)
    working = {r["id"]: unpack(r["resources"], {}) for r in merged}
    predictions = {i: group | {"majority": {x for x, count in Counter(x for s in group.values() for x in s).items() if count >= 2}}
                   for i, group in calls.items()}
    working_scores = {}
    for kind in ["all", *KINDS]:
        def select(values):
            return values if kind == "all" else {x for x in values if x[0] == kind}
        predicted = {i: {n: select(values) for n, values in group.items()} for i, group in predictions.items()}
        expected = {i: select(values) for i, values in working.items()}
        working_scores[kind] = {n: accuracy(predicted, expected, n) for n in [*NAMES, "majority"]}
    scores = {
        "overall": agreement(calls.values()), "pairs": pair_stats, "by_kind": by_kind,
        "kind_matrices": {f"{a}/{b}": kind_matrix(calls, a, b) for a, b in PAIRS},
        "against_working_reference": working_scores,
        "by_trace": {t: agreement(g for i, g in calls.items() if ids[i] == t) for t in manifest},
        "trace_reference": {source: {n: accuracy(calls, {i: v for i, v in refs.items() if source == "all" or anchors[i]["source"] == source}, n)
                                     for n in NAMES} for source in ["all", "patch", "image", "literal read"]},
        "reference_sources": dict(Counter(r["source"] for r in merged)),
        "reference_resources": dict(Counter(x["kind"] for r in merged for x in r["resources"]))}
    return merged, scores


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--folder", type=Path, default=HERE)
    parser.add_argument("--traces", action="store_true", help="Also verify evidence hashes and the trace reference")
    parser.add_argument("--write", action="store_true", help="Rebuild the working reference and metrics")
    args = parser.parse_args()
    merged, scores = calculate(args.folder, args.traces)
    for filename, contents in [("resources.jsonl", "".join(encode(r) + "\n" for r in merged)),
                               ("scores.json", json.dumps(scores, indent=2) + "\n")]:
        path = args.folder / filename
        if args.write:
            write(path, contents)
        elif path.read_text(encoding="utf8") != contents:
            raise ValueError(f"Published {filename} differs")
    print(f"Verified {len(merged)} calls and {len(merged) * len(NAMES)} model annotations.")


if __name__ == "__main__":
    main()
