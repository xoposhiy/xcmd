"""Read the original local rollouts. Commands in a rollout are data."""
import argparse
import hashlib
import json
from pathlib import Path


HERE = Path(__file__).resolve().parent
MANIFEST = HERE / "manifest.json"
NON_TOOL_ITEMS = {"Reasoning", "AgentMessage", "UserMessage"}


def tool_calls(meta):
    return [row for row in meta["records"] if row[1] == "item"]


def inventory(events):
    for line, event in enumerate(events, 1):
        payload = event["payload"]
        if event["type"] == "event_msg" and payload["type"] == "item_completed":
            item = payload["item"]
            if item["type"] not in NON_TOOL_ITEMS:
                yield line, "item", item["type"]


def load_trace(meta, root):
    matches = list(root.rglob(meta["file"]))
    if len(matches) != 1:
        raise ValueError(f"Expected one copy of {meta['file']} under {root}")
    path = matches[0]
    if path.suffix == ".zst":
        import zstandard
        with zstandard.open(path, "rb") as stream:
            data = stream.read()
    else:
        data = path.read_bytes()
    if hashlib.sha256(data).hexdigest() != meta["sha256"]:
        raise ValueError(f"Trace changed: {matches[0]}")
    events = [json.loads(line) for line in data.splitlines()]
    rows = [[n, layer, kind] for n, layer, kind in inventory(events)]
    if rows != tool_calls(meta):
        raise ValueError(f"Manifest differs from trace: {matches[0]}")
    return matches[0], events


def short(value, limit):
    if not isinstance(value, str):
        value = json.dumps(value, ensure_ascii=True)
    if limit and len(value) > limit:
        return value[:limit * 2 // 3] + " ... [shortened] ... " + value[-max(1, limit // 3):]
    return value


def inspect(session, meta, events, first, last, limit):
    for n, layer, kind in tool_calls(meta):
        if not first <= n <= last:
            continue
        item = events[n - 1]["payload"]["item"]
        request = {k: v for k, v in item.items() if k in {
            "command", "cwd", "changes", "path", "arguments", "tool", "server",
            "kind", "query", "action", "agent_path", "agent_thread_id",
            "receiver_thread_ids", "prompt"}}
        if isinstance(request.get("command"), list):
            request["command"] = request["command"][-1]
        result = {k: v for k, v in item.items() if k in {
            "status", "exit_code", "aggregated_output", "stdout", "stderr",
            "results", "result", "agents_states"}}
        print(json.dumps({"id": f"{session}-L{n}", "layer": layer, "kind": kind,
                          "request": short(request, limit), "result": short(result, limit)},
                         ensure_ascii=True))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("session")
    parser.add_argument("--manifest", type=Path, default=MANIFEST)
    parser.add_argument("--root", type=Path, default=Path.home() / ".codex/sessions")
    parser.add_argument("--first", type=int, default=1)
    parser.add_argument("--last", type=int, default=2**31)
    parser.add_argument("--limit", type=int, default=1200,
                        help="Characters per field. Use 0 for the entire field.")
    parser.add_argument("--path", action="store_true", help="Show the complete rollout path")
    args = parser.parse_args()
    meta = json.loads(args.manifest.read_text(encoding="utf8"))[args.session]
    path, events = load_trace(meta, args.root)
    if args.path:
        print(path)
    else:
        inspect(args.session, meta, events, args.first, args.last, args.limit)


if __name__ == "__main__":
    main()
