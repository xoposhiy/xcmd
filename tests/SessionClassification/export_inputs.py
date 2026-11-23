# @tests InspectAgentSession
"""Build portable, label-free transcripts from the six verified study traces."""
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE / "activity-target-study/resources"))
from annotate import MANIFEST, clean, evidence, load_trace


def excerpt(text, limit):
    if len(text) <= limit:
        return text
    return text[:limit * 2 // 3] + " [...] " + text[-limit // 3:]


def export(name, meta, rows):
    _, events = load_trace(meta, Path.home() / ".codex/sessions")
    root = next(e["payload"]["cwd"] for e in events if e["type"] == "session_meta")
    calls = {int(row["id"].rsplit("-L", 1)[1]): row for row in rows}
    lines = []
    for number, event in enumerate(events, 1):
        lines.append("")
        payload = event["payload"]
        if event["type"] != "event_msg" or payload.get("type") != "item_completed":
            continue
        item = payload["item"]
        kind = item["type"]
        if number in calls:
            row = calls[number]
            record = {"type": kind, "cwd": row["cwd"], "status": item.get("status", "")}
            if kind == "CommandExecution":
                record.update(command=["pwsh", "-Command", row["command"]], exit_code=row["exit"])
                if "result" in row:
                    record["aggregated_output"] = row["result"]
            elif kind == "FileChange":
                record["changes"] = {clean(path, root): {
                    "type": change["type"], "move_path": clean(change.get("move_path") or "", root),
                    "unified_diff": excerpt(clean(change.get("unified_diff") or change.get("content") or "", root), 2000)
                } for path, change in item["changes"].items()}
            elif kind == "ImageView":
                record["path"] = row["path"]
        elif kind in ("UserMessage", "AgentMessage"):
            words = " ".join(c.get("text", "") for c in item.get("content", []))
            record = {"type": kind, "content": [{"text": excerpt(clean(words, root), 1200)}]}
        else:
            continue
        lines[-1] = json.dumps({"timestamp": event["timestamp"], "type": "event_msg",
                                "payload": {"type": "item_completed", "item": record}},
                               ensure_ascii=False, separators=(",", ":"))
    path = HERE / "transcripts" / (name + ".jsonl")
    path.parent.mkdir(exist_ok=True)
    path.write_text("\n".join(lines).rstrip("\n") + "\n", encoding="utf8", newline="\n")


if __name__ == "__main__":
    manifest = json.loads(MANIFEST.read_text(encoding="utf8"))
    for session, _, evidence_rows in evidence():
        export(session, manifest[session], evidence_rows)
