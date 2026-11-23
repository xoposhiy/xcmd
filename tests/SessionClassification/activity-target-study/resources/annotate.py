"""Collect independent resource annotations for the software development corpus."""
import argparse
import hashlib
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from urllib.parse import unquote

HERE = Path(__file__).resolve().parent
STUDY = HERE.parent
ROOT = STUDY.parents[2]
MANIFEST = STUDY / "terra-luna/software/manifest.json"
sys.path.insert(0, str(STUDY / "terra"))
from trace import load_trace, tool_calls

MODELS = {"terra": ("gpt-6.1-terra", "none"), "sol": ("gpt-6.2-sol", "low"), "luna": ("gpt-6-luna", "none")}
KINDS = ["file", "directory", "url", "issue", "repository", "other"]
GUIDE = (HERE / "guide.txt").read_text(encoding="utf8").strip()


def encode(value):
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"))


def digest(value):
    return hashlib.sha256(value.encode("utf8")).hexdigest()


def write(path, value):
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(value, encoding="utf8", newline="\n")
    tmp.replace(path)


def clean(text, root):
    def root_pattern(value):
        return r"[\\/]".join(re.escape(p) for p in value.replace("\\", "/").split("/"))
    text = re.sub(r"(?i)file:///(?=[A-Z]:)", "", text)
    for path, label in [(root, "."), (str(Path(root).parent), ".."),
                        (str(Path.home() / "AppData/Local/Temp"), "%TEMP%"),
                        (str(Path.home() / "AppData/Local"), "%LOCALAPPDATA%"), (str(Path.home()), "~")]:
        text = re.sub(root_pattern(path) + r"(?=$|[\\/\s'\",;)])", lambda _: label, text, flags=re.I)
    text = re.sub(r"(?i)(?:sk-[\w-]{14,}|gh[pousr]_\w{14,}|github_pat_\w{14,}|AIza[\w-]{30,}|\d{8,}:[\w-]{28,})", "[redacted]", text)
    text = re.sub(r"(?i)((?:api[_-]?key|access[_-]?token|authorization|password)[\"']?\s*[:=]\s*[\"']?)(?:Bearer\s+)?[^\s\"',;\]}]{10,}", r"\1[redacted]", text)
    return re.sub(r"[A-Za-z0-9+/=]{180,}", "[encoded data]", text)


def evidence():
    sessions = json.loads(MANIFEST.read_text(encoding="utf8"))
    for trace, meta in sessions.items():
        _, events = load_trace(meta, Path.home() / ".codex/sessions")
        root = next(e["payload"]["cwd"] for e in events if e["type"] == "session_meta")
        rows = []
        for line, _, kind in tool_calls(meta):
            item = events[line - 1]["payload"]["item"]
            row = {"id": f"{trace}-L{line}", "tool": kind,
                   "cwd": clean(unquote(item.get("cwd", root)), root).replace("\\", "/")}
            if kind == "CommandExecution":
                command = clean(item["command"][-1], root)
                out = clean(item.get("aggregated_output") or "", root)
                row.update(command=command, exit=item.get("exit_code"))
                if re.search(r"\brg\b", command):
                    hits = re.findall(r"(?m)^((?:[A-Za-z]:)?[^:\r\n<>|*?\"]{1,280}?\.[A-Za-z0-9_-]{1,15}):\d+:", out)
                    row["search_hits"] = list(dict.fromkeys(p.replace("\\", "/") for p in hits))
                # Diagnostic dumps can include private messages and database rows.
                if not re.search(r"sqlite3|data[/\\](?:runtime-logs|startup-logs|extraction-logs|agent-runs)|point-taken (?:list|show)", command, re.I):
                    row["result"] = out if len(out) <= 1600 else out[:1000] + "\n[...]\n" + out[-600:]
            elif kind == "FileChange":
                row["status"] = item["status"]
                row["changes"] = [[clean(p, root), change["type"], clean(change.get("move_path") or "", root)]
                                  for p, change in item["changes"].items()]
            elif kind == "ImageView":
                row["path"] = clean(unquote(item["path"]), root)
            else:
                raise ValueError(f"Missing evidence projection for {kind}")
            rows.append(row)
        yield trace, meta["project"], rows


def object_schema(properties):
    return {"type": "object", "properties": properties, "required": list(properties), "additionalProperties": False}


RESOURCE = object_schema({"kind": {"type": "string", "enum": KINDS}, "value": {"type": "string"}})
ROW = object_schema({"id": {"type": "string"}, "resources": {"type": "array", "items": RESOURCE}})
SCHEMA = object_schema({"rows": {"type": "array", "items": ROW}})


def batches(rows):
    packet = []
    for row in rows:
        if packet and (len(packet) >= 8 or len(encode([*packet, row])) > 19000):
            yield packet
            packet = []
        packet.append(row)
    if packet:
        yield packet


def run_batch(key, cache, reviewer, project, rows):
    model, effort = MODELS[reviewer]
    body = {"model": model, "reasoning": {"effort": effort}, "store": False, "instructions": GUIDE,
            "input": encode({"project": project, "calls": rows}), "max_output_tokens": 16000,
            "text": {"format": {"type": "json_schema", "strict": True, "name": "call_resources", "schema": SCHEMA}}}
    fingerprint = digest(encode(body))
    destination = cache / (fingerprint + ".json")
    if destination.is_file():
        return json.loads(destination.read_text(encoding="utf8"))
    req = urllib.request.Request("https://api.openai.com/v1/responses", data=encode(body).encode("utf8"),
                                 headers={"Authorization": "Bearer " + key, "Content-Type": "application/json"})
    ids = [r["id"] for r in rows]
    for attempt in range(4):
        try:
            with urllib.request.urlopen(req, timeout=240) as response:
                reply = json.load(response)
            if reply["status"] != "completed":
                raise ValueError(f"Incomplete response: {reply.get('incomplete_details')}")
            output = "".join(c["text"] for m in reply["output"] for c in m.get("content", []) if c["type"] == "output_text")
            labels = json.loads(output)["rows"]
            if sorted(r["id"] for r in labels) != sorted(ids):
                raise ValueError("Response has different call IDs")
            if any(v["kind"] not in KINDS or not v["value"].strip() for r in labels for v in r["resources"]):
                raise ValueError("Invalid resource")
            result = {"reviewer": reviewer, "model": reply["model"], "effort": effort, "request_sha256": fingerprint,
                      "input_sha256": digest(body["input"]), "response_id": reply["id"], "usage": reply["usage"], "rows": labels}
            write(destination, encode(result))
            print(reviewer, ids[0], len(ids), flush=True)
            return result
        except urllib.error.HTTPError as error:
            if error.code not in (408, 429, 500, 502, 503, 504):
                raise
            if attempt == 3:
                raise
            time.sleep(max(2 ** attempt, int(error.headers.get("retry-after") or 0)))
        except (TimeoutError, urllib.error.URLError, ValueError):
            if attempt == 3:
                raise
            time.sleep(2 ** attempt)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cache", type=Path, default=ROOT / "artifacts/resource-study")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--workers", type=int, default=6)
    args = parser.parse_args()
    key = os.getenv("OPENAI_API_KEY")
    if not key:
        key = re.search(r"(?m)^OPENAI_API_KEY=(.+)$", (ROOT / ".env").read_text(encoding="utf-8-sig")).group(1).strip().strip("\"'")
    args.cache.mkdir(parents=True, exist_ok=True)
    args.output.mkdir(parents=True, exist_ok=True)
    jobs = [(name, project, packet) for _, project, rows in evidence() for packet in batches(rows) for name in MODELS]
    with ThreadPoolExecutor(args.workers) as pool:
        replies = list(pool.map(lambda job: run_batch(key, args.cache, *job), jobs))
    receipt = {"guide_sha256": digest(GUIDE), "manifest_sha256": digest(MANIFEST.read_text(encoding="utf8")),
               "requests": [{k: v for k, v in r.items() if k != "rows"} |
                            {"ids": [row["id"] for row in r["rows"]], "labels_sha256": digest(encode(r["rows"]))}
                            for r in replies]}
    for name in MODELS:
        write(args.output / f"{name}.jsonl", "".join(encode(row) + "\n" for r in replies if r["reviewer"] == name for row in r["rows"]))
    write(args.output / "receipt.json", json.dumps(receipt, indent=2) + "\n")


if __name__ == "__main__":
    main()
