"""Export a compact, label-free record from each completed command or patch."""
import hashlib
import json
import ntpath
import re
import subprocess
from pathlib import Path
from urllib.parse import unquote, urlsplit

from classifier import MARKERS, identity, operations

def recorded_path(value):
    if not value.lower().startswith("file:"):
        return value
    uri = urlsplit(value)
    path = unquote(uri.path)
    if uri.netloc and uri.netloc.lower() != "localhost":
        return "//"+uri.netloc+path
    return re.sub(r"^/(?=[A-Za-z]:/)", "", path)

def scrub(text, repo):
    clean = text.replace("\r","")
    for form in {str(repo),str(repo).replace("\\","/"),str(repo).replace("/","\\")}:
        clean = re.sub(re.escape(form)+r"(?=[/\\\s'\";]|$)", "/repo", clean, flags=re.I)
    clean = re.sub(r"(?i)(?:[a-z]:[/\\]users|/home|/users)[/\\][^/\\\s'\"]+", "/home/reader", clean)
    clean = re.sub(r"https?://[^\s\"']+", "[URL omitted]", clean)
    clean = re.sub(r"(?i)(?:sk-|github_pat_|ghp_|xox[baprs]-|AKIA)[\w-]{10,}", "[credential omitted]", clean)
    clean = re.sub(r"(?im)(?:^|[\s;])(?:[\w.-]*(?:password|api[_-]?key|access[_-]?token|authorization)[\w.-]*)\s*[:=]\s*[^\r\n;]+"," [value omitted]",clean)
    clean = re.sub(r"\b[A-Za-z0-9+/=_-]{48,}\b","[opaque value omitted]",clean)
    clean = re.sub(r"(?i)(--note|-m|--message) (['\"]).*?\2", r"\1 '[message omitted]'",clean)
    return clean

def output_signals(output):
    lines = output.splitlines()
    signals = [marker for marker in MARKERS if any(marker in line for line in lines[:150])]
    for line in lines[:12]:
        text = line.strip()
        if re.match(r"^(?:rg:|fatal:|error:|warning:|Traceback|FAILED |passed\b|OK$|## |\[main |Building \d|Determining projects)", text, re.I):
            signals.append(text[:130])
        elif re.fullmatch(r"(?:src|tests|docs|scripts|tools)[/\\][\w./\\-]+",text):
            signals.append(text[:120])
        elif re.match(r"^\S+:\d+:",text):
            signals.append(re.sub(r"(:\d+:).*",r"\1 [match]",text)[:120])
        if len(signals) >= 4:
            break
    return list(dict.fromkeys(signals))[:6]

def tree(repo, rev):
    if not re.fullmatch(r"[a-fA-F0-9]{7,64}",rev or ""):
        return None
    reply = subprocess.run(["git","-c","safe.directory="+repo.as_posix(),"-C",str(repo),"ls-tree","-r","-z","--name-only",rev],
                           capture_output=True)
    if reply.returncode:
        return None
    files = {"/repo/"+x.decode("utf8",errors="replace") for x in reply.stdout.split(b"\0") if x}
    dirs = {p[:m.start()] for p in files for m in re.finditer("/",p) if m.start()}
    if ntpath.splitdrive(str(repo))[0]:
        return {p.casefold() for p in files}, {p.casefold() for p in dirs}
    return files,dirs

def export_trace(meta):
    file,repo = Path(meta["trace"]),Path(meta["repo"])
    rows = []
    with file.open(encoding="utf8") as f:
        for n,line in enumerate(f,1):
            event = json.loads(line)
            payload = event.get("payload",{})
            if n == 1:
                base = recorded_path(payload.get("cwd",str(repo)))
                index = tree(repo,(payload.get("git") or {}).get("commit_hash",""))
            item = payload.get("item") or {}
            if payload.get("type") != "item_completed" or item.get("type") not in {"CommandExecution","FileChange"}:
                continue
            tool = item["type"]
            cwd = scrub(recorded_path(item.get("cwd") or base),repo).replace("\\","/").rstrip("/") or "/"
            changes = []
            for path,change in (item.get("changes") or {}).items():
                entry = {"kind":change["type"],"path":scrub(recorded_path(path),repo).replace("\\","/")}
                if change.get("move_path"):
                    entry["to"] = scrub(recorded_path(change["move_path"]),repo).replace("\\","/")
                changes.append(entry)
            command = item.get("command")
            command = command[-1] if isinstance(command,list) and command else command or ""
            command = scrub(command,repo)
            if tool == "FileChange":
                command = "\n".join(c["kind"]+" "+c["path"] for c in changes)
            output = item.get("aggregated_output") or ""
            # Retain both ends of long commands for the classifier. Never execute a recorded command.
            if len(command) > 2500:
                command = command[:1700]+"\n[command shortened]\n"+command[-700:]
            row = dict(id=meta["key"]+"-L"+str(n),session=meta["key"],tool=tool,command=command,cwd=cwd,
                       windows=bool(ntpath.splitdrive(base)[0]),status=item.get("status"),exit=item.get("exit_code"),
                       changes=changes,start=payload["started_at_ms"],end=payload["completed_at_ms"],
                       output_lines=sum(bool(s.strip()) for s in output.splitlines()),
                       excerpt=[scrub(s,repo) for s in output_signals(output)])
            facts = []
            for path in operations(row)[1]:
                key = identity(row,path)
                kind = ("file at session start" if index and key in index[0] else
                        "directory at session start" if index and key in index[1] else None)
                if kind:
                    facts.append({"operand":path,"kind":kind})
            row["path_facts"] = facts[:12]
            rows.append(row)
    return rows

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
