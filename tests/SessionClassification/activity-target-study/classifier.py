"""Classify recorded tool calls. This module only consumes observations."""
import posixpath
import re
import shlex
from pathlib import Path, PurePosixPath

BASE = Path(__file__).resolve().parent
PROMPT = (BASE / "prompt.txt").read_text(encoding="utf-8").strip()
TARGETS = ["source", "tests", "docs", "tooling", "git", "tracker", "mixed", "unknown"]
ACTIONS = ["read", "write", "other", "unknown"]
FILE_ROLES = {"source", "tests", "docs", "tooling"}
SCHEMA = {"type": "object", "properties": {
    "target": {"type": "string", "enum": TARGETS},
    "action": {"type": "string", "enum": ACTIONS}, "cue": {"type": "string"}},
    "required": ["target", "action", "cue"], "additionalProperties": False}
READ = {"get-content", "cat", "type", "head", "tail"}
LIST = {"ls", "dir", "get-childitem", "rg", "grep", "pwd", "get-location"}
MARKERS = {"import pytest": "tests", "from pytest import": "tests", "[Fact]": "tests",
           "[Theory]": "tests", "using Xunit;": "tests", "import unittest": "tests",
           "from unittest import TestCase": "tests", "Microsoft.NET.Test.Sdk": "tooling",
           "WebApplication.CreateBuilder(": "source", "ReactDOM.createRoot(": "source"}
GIT_READ = {"diff", "status", "ls-files", "ls-tree", "log", "show", "rev-parse",
            "grep", "blame", "describe", "cat-file", "remote", "reflog"}
TRACK_READ = {"list", "show", "view", "search", "status"}
TRACK_WRITE = {"start", "close", "create", "edit", "reopen", "comment", "update"}
UNKNOWN = {"target": "unknown", "action": "unknown", "cue": "Unusable model response"}

def identity(row, path):
    path = path.replace("\\", "/")
    if any(c in path for c in "$*?{}<>|") or "://" in path:
        return None
    path = posixpath.normpath(posixpath.join(row["cwd"], path)) if not re.match(r"^\w:/", path) else posixpath.normpath(path)
    return path.casefold() if row["windows"] else path

def path_role(path):
    if not path:
        return None
    p = PurePosixPath(path.lower())
    parts = p.parts
    inside = parts[0] == "/" and len(parts) > 1 and parts[1] == "repo"
    if ".nuget" in parts or ".agents" in parts and "skills" in parts or "node_modules" in parts:
        return "tooling"
    if not inside:
        return None
    dirs = parts[2:-1]
    if p.name in {"agents.md", "readme", "readme.md", "contributing.md", "changelog.md"} or "docs" in dirs or p.name == "docs":
        return "docs"
    if p.suffix in {".sln", ".slnx", ".csproj", ".props", ".targets"} or p.name in {
        "package.json", "package-lock.json", "pyproject.toml", "cargo.toml", "cargo.lock",
        "makefile", "dockerfile", "nuget.config", "global.json"}:
        return "tooling"
    if set(parts[2:]) & {"scripts", "tools", ".github", ".agents", "node_modules", ".nuget"}:
        return "tooling"
    if set(parts[2:]) & {"test", "tests", "spec", "fixtures", "__tests__"} or re.search(
            r"(^test_.+\.py$|_test\.(py|go)$|\.(spec|test)\.[cm]?[jt]sx?$)", p.name):
        return "tests"
    if set(parts[2:]) & {"src", "app", "lib"}:
        return "source"
    return None

def words(text):
    lexer = shlex.shlex(text, posix=False, punctuation_chars="|;&<>")
    lexer.whitespace_split = True
    lexer.commenters = ""
    try:
        return [token.strip("\"'") for token in lexer]
    except ValueError:
        return []

def operations(row):
    if row["tool"] == "FileChange":
        return "patch", [c["path"] for c in row["changes"]], []
    raw = row["command"]
    if "\n" in raw or "$(" in raw or "\x60" in raw:
        return "script", [], []
    tokens = words(raw)
    if not tokens:
        return "script", [], []
    if any(t in {"&", "&&", ";", "||", ">", ">>", "<", "<<"} for t in tokens):
        return "script", [], []
    if "|" in tokens:
        start = tokens.index("|")
        tail = tokens[start+1:]
        if not tail or tail[0].lower() not in {"select-object", "sort-object", "rg", "grep", "head", "tail"} or "|" in tail:
            return "script", [], []
        # A second grep with explicit file operands is an independent input.
        if tail[0].lower() in {"rg", "grep"} and len(tail) != 2:
            return "script", [], []
        if any(x in raw for x in ("{", "}", "$")):
            return "script", [], []
        tokens = tokens[:start]
    head, args = tokens[0].lower(), tokens[1:]
    if head not in READ | LIST:
        return head, [], args
    take_value = {"-encoding", "-totalcount", "-skip", "-first", "-last", "-n" if head in {"head","tail"} else "--max-count",
                  "-g", "--glob", "-m", "-c" if head == "rg" else "--context", "-C", "--max-columns", "--type", "-t"}
    paths = []
    args_iter = iter(args)
    for arg in args_iter:
        if arg in take_value or arg.lower() in {"-encoding","-totalcount"}:
            next(args_iter, None)
        elif arg.startswith("-"):
            if arg in {"-e", "-f", "--file", "--regexp", "--pathspec-from-file", "--pre", "--pre-glob"}:
                return "script", [], []
        elif head.startswith("get-"):
            paths.extend(arg.split(","))
        else:
            paths.append(arg)
    if head in {"rg", "grep"} and "--files" not in args:
        paths = paths[1:]
    return head, paths, args

def result(target, action, cue):
    return {"target": target, "action": action, "cue": cue}

def local(row, op, paths, args):
    ok = row["status"] == "completed" and row["exit"] in (None, 0)
    roles = [path_role(identity(row, p)) for p in paths]
    if op in {"git", "git.exe"} and args:
        sub = args[0].lower()
        if any(a == "--output" or a.startswith("--output=") for a in args):
            return None
        if sub in GIT_READ - {"remote", "reflog"} or sub in {"remote","reflog"} and len(args) == 1:
            return result("git","read","Git query: "+sub)
        if sub in {"commit","add"} and "--dry-run" not in args and "-n" not in args:
            return result("git","write" if ok else "other","Git mutation: "+sub)
    if op == "point-taken" or op == "gh" and args[:1] == ["issue"]:
        sub = (args if op == "point-taken" else args[1:])[:1]
        if sub and sub[0] in TRACK_READ | TRACK_WRITE:
            return result("tracker","read" if sub[0] in TRACK_READ else ("write" if ok else "other"),"Issue command: "+sub[0])
    if op == "patch" or op in READ | LIST:
        if op in LIST and not paths:
            target = "mixed" if row["cwd"] == "/repo" else path_role(identity(row,"."))
        elif roles and all(roles):
            target = roles[0] if len(set(roles)) == 1 else "mixed"
        elif op in LIST and len(paths) == 1 and identity(row,paths[0]) == "/repo":
            target = "mixed"
        elif op in READ and ok and len(paths) == 1 and identity(row,paths[0]) and row["output_lines"]:
            # Attribute and startup markers can identify a role even for a neutral filename.
            strong = {MARKERS[m] for m in ("[Fact]","[Theory]","WebApplication.CreateBuilder(","ReactDOM.createRoot(") if m in row["excerpt"]}
            target = strong.pop() if len(strong) == 1 else None
            if target:
                return result(target,"read","Direct file read with a role-specific content marker")
        else:
            target = None
        if target:
            return result(target, ("write" if ok else "other") if op == "patch" else "read",
                          "Recorded file change" if op == "patch" else "Query scope: "+op)
    if op in {"dotnet","npm","cargo"} and args and args[0] in {"test","build"}:
        return result("tests" if args[0]=="test" else "tooling","other","Run "+args[0])
    return None

def packet(row, history):
    current = {key: row[key] for key in ("tool","cwd","command","status","exit","excerpt","changes")}
    current["output_lines"] = row["output_lines"]
    if row.get("path_facts"):
        current["path_operands"] = row["path_facts"]
    data = {"current":current}
    if history:
        data["prior_file_roles"] = history
    return data

class SessionMemory:
    def __init__(self):
        self.files = {}
        self.pending = []
        self.last_barrier = -1
        self.written = {}
        self.sequence = 0

    def advance(self, start):
        ready = sorted((x for x in self.pending if x[0] <= start), key=lambda x:(x[0],x[1]))
        self.pending = [x for x in self.pending if x[0] > start]
        for end, index, key, entry, began, mutation in ready:
            if key is None:
                self.files.clear()
                self.last_barrier = end
            elif began >= self.last_barrier and (mutation or began >= self.written.get(key,-1)):
                if mutation:
                    self.written[key] = end
                if entry:
                    self.files[key] = entry
                else:
                    self.files.pop(key,None)

    def lookup(self, row, paths):
        keys = dict.fromkeys(identity(row,p) for p in paths)
        changing = {key for _,_,key,_,_,mutation in self.pending if mutation}
        if None in changing:
            return []
        return [dict(path=k, **self.files[k]) for k in keys if k in self.files and k not in changing][:12]

    def observe(self, row, op, paths, answer):
        def queue(key, entry, mutation=False):
            self.sequence += 1
            self.pending.append((row["end"],self.sequence,key,entry,row["start"],mutation))
        if row["status"] != "completed" or row["exit"] not in (0,None):
            return
        if re.search(r"\bgit\s+(?:checkout|switch|reset|rebase|merge|pull|clean)\b|\b(?:mv|move-item|rm|remove-item|rename-item)\b",row["command"],re.I):
            queue(None,None,True)
            return
        if op == "patch":
            for change in row["changes"]:
                for path in (change["path"], change.get("to")):
                    if path and (key := identity(row,path)):
                        role = path_role(key)
                        live = path == (change.get("to") or change["path"]) and change["kind"] != "delete"
                        entry = dict(target=role, observed_in=row["id"], evidence="confirmed change and path rule") if live and role else None
                        queue(key,entry,True)
            return
        if op not in READ or len(paths) != 1 or not row["output_lines"]:
            return
        key = identity(row,paths[0])
        if key is None:
            return
        roles = {role for marker,role in MARKERS.items() if marker in row["excerpt"]}
        supported = roles.pop() if len(roles) == 1 else path_role(key) if not roles else None
        if supported in FILE_ROLES and answer["target"] == supported and answer["action"] == "read":
            queue(key,dict(target=supported, observed_in=row["id"],
                           evidence="content marker" if any(m in row["excerpt"] for m in MARKERS) else "direct read and path rule"))
        else:
            queue(key,None)
