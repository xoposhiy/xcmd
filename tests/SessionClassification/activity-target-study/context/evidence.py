"""Derive bounded facts from recorded syntax and the historical Git tree."""
import ast
from bisect import bisect_right
import collections
from datetime import datetime
import functools
import json
import re
import subprocess
import tempfile
from pathlib import Path

from classifier import identity, path_role, operations, MARKERS
from inputs import recorded_path

HERE = Path(__file__).resolve().parent
EXTENSIONS = {
    "code":set("py js ts jsx tsx java kt cs go rb cpp c h hpp rs css html vue svelte asm fs lua swift".split()),
    "documentation":set("md markdown rst adoc pdf docx pptx tex".split()),
    "data":set("json jsonl yaml yml csv tsv xml xlsx parquet db sqlite ndjson ipynb".split()),
    "script":set("sh bash cmd bat ps1 psm1".split())}
PATH = re.compile(r"""(?<![\w.])((?:[A-Za-z]:)?(?:[\w.@~-]+[/\\])*[\w@~-][\w.@~-]*\.(?:[A-Za-z0-9]{1,9}))(?=$|[\s,'"\x60():;|\]}>])""")
PROJECT_SIGNALS = {
    "teaching or course material": ("curriculum","syllabus","lecture","course materials","учебн","лекци","студент"),
    "research or analysis": ("benchmark","experiment","research","analysis","исследован","эксперимент","анализ"),
    "game or simulation": ("game","simulation","leaderboard","игра","симуляц"),
    "developer utility": ("command line","command-line","developer tool","automation","cli tool","плагин"),
    "website": ("website","web app","web application","сайт"),
    "documents or publishing": ("publication","documentation","markdown","публикац","документац")}
CONTEXT = {
    "documentation":r"\b(?:readme|documentation|slides|syllabus)\b|документ|презентаци|лекци",
    "verification":r"\b(?:test|tests|verify|benchmark|validation)\b|проверь|провер[ик]|тест",
    "code change":r"\b(?:implement|fix|refactor)\b|реализ|исправ|отрефактор",
    "data analysis":r"\b(?:analyze|analyse|analysis|dataset|statistics|results)\b|анализ|статистик|результат",
    "issue work":r"\b(?:ticket|issue tracker)\b|трекер",
    "repository work":r"\b(?:commit|branch|pull request)\b|коммит"}
def kind(path):
    ext=Path(path).suffix.lower().lstrip(".")
    return next((k for k,v in EXTENSIONS.items() if ext in v),None)

def git(repo,*args):
    p=subprocess.run(["git","-c","safe.directory="+repo.as_posix(),"-C",str(repo),*args],capture_output=True)
    return p.stdout if p.returncode==0 else b""

class Catalog:
    def __init__(self,repo,revision,windows):
        self.repo=repo
        self.files={}
        self.fold = str.casefold if windows else str
        if re.fullmatch(r"[a-f0-9]{7,64}",revision or ""):
            for line in git(repo,"ls-tree","--full-tree","-r","-l","-z",revision).split(b"\0"):
                meta,tab,name=line.partition(b"\t")
                if not tab:continue
                fields=meta.split()
                if len(fields)!=4 or fields[1]!=b"blob" or not fields[3].isdigit():continue
                self.files[self.fold(name.decode("utf8","replace"))]=(fields[2].decode(),int(fields[3]))
        counts=collections.Counter(kind(p) for p in self.files)
        self.profile={"tracked_files_by_kind":{k:v for k,v in counts.items() if k}}
        readme=next((self.text(p) for p in ("README.md","README.rst","README","readme.md") if self.fold(p) in self.files),"")
        self.profile["readme_topics"]=[k for k,words in PROJECT_SIGNALS.items() if any(w in readme[:9000].lower() for w in words)]
        self.entries=set()
        try:
            package=json.loads(self.text("package.json") or "{}")
            for field in ("bin","main","module"):
                val=package.get(field)
                for s in ((val,) if isinstance(val,str) else val.values() if isinstance(val,dict) else ()):
                    if isinstance(s,str):self.entries.add(self.fold(s.removeprefix("./")))
        except (ValueError,AttributeError):
            pass
    @functools.lru_cache(maxsize=512)
    def text(self,path):
        rec=self.files.get(self.fold(path))
        if not rec or rec[1]>180000:return ""
        return git(self.repo,"cat-file","blob",rec[0]).decode("utf8","replace")
    def fact(self,row,path):
        key=identity(row,path)
        if not key:return None
        name=self.fold(key.removeprefix("/repo/")) if key.startswith("/repo/") else None
        seen=name in self.files if name else False
        words=[]
        if name in self.entries:words.append("package entry point")
        if seen and kind(name) in {"code","script"}:
            data=self.text(name)
            for flag,rx in [
              ("test definitions",r"(?m)^\s*(?:def test_\w+|func Test\w+\(|\[(?:Fact|Theory)\]|\s*(?:it|describe|test)\(['\"])"),
              ("command-line interface",r"ArgumentParser\(|System\.CommandLine|commander\.|process\.argv|flag\.Parse\("),
              ("application startup",r"WebApplication\.CreateBuilder|ReactDOM\.createRoot|Application\.Run\("),
              ("analytical code",r"\b(?:import pandas|import matplotlib|from scipy|SELECT\s+COUNT)\b"),
              ("developer automation",r"dotnet (?:build|test|publish)|npm run (?:test|build)|cargo (?:test|build)")]:
                if re.search(rx,data):words.append(flag)
        return {"operand":path,"at_start":"tracked file" if seen else "unverified",
                **({"file_kind":kind(path)} if kind(path) else {}),**({"markers":words} if words else {})}

def powershell(rows):
    with tempfile.TemporaryDirectory() as tmp:
        p=Path(tmp)/"commands.json"
        p.write_text(json.dumps([{"id":r["id"],"command":r["command"]} for r in rows]),encoding="utf8")
        out=subprocess.run(["pwsh","-NoLogo","-NoProfile","-NonInteractive","-File",str(HERE/"syntax.ps1"),str(p)],capture_output=True,check=True)
    return json.loads(out.stdout.decode("utf-8-sig"))

def trace_signals(meta):
    prompts=[]
    prompt_times=[]
    by_id={}
    revision=""
    with Path(meta["trace"]).open(encoding="utf8") as f:
        for n,line in enumerate(f,1):
            event=json.loads(line);p=event.get("payload",{})
            if n==1:revision=(p.get("git") or {}).get("commit_hash","")
            if event.get("type")=="event_msg" and p.get("type")=="user_message":
                text=p.get("message","")
                if isinstance(text,str) and not re.search(r"AGENTS\.md instructions|<environment_context>|<turn_aborted>",text):
                    stamp=event.get("timestamp")
                    if stamp:
                        t=int(datetime.fromisoformat(stamp.replace("Z","+00:00")).timestamp()*1000)
                        pos=bisect_right(prompt_times,t)
                        prompt_times.insert(pos,t)
                        prompts.insert(pos,[k for k,rx in CONTEXT.items() if re.search(rx,text[:6000],re.I)])
            item=p.get("item") or {}
            if p.get("type")!="item_completed" or item.get("type") not in {"CommandExecution","FileChange"}:continue
            out=(item.get("aggregated_output") or "")[:16000]
            shapes=[]
            for tag,rx in [("test summary",r"(?im)(?:\d+ passed|all tests passed|tests run:|total tests:)"),
                           ("build summary",r"(?im)(?:build succeeded|build failed|compilation finished)"),
                           ("directory listing",r"(?im)(?:LastWriteTime\s+Length\s+Name|Directory:\s|^total \d+$)"),
                           ("table or query results",r"(?m)(?:^\S+(?:\t\S+){2,}|^-{3,}\s+-{3,})"),
                           ("structured data",r"^\s*[\[{]\s*[{\x22]"),("diff or commit",r"(?m)^(?:diff --git |@@ -\d|\[[\w/-]+ [a-f0-9]{7,})"),
                           ("rendered or generated output",r"(?im)^(?:wrote|saved|rendered|generated|exported)\b")]:
                if re.search(rx,out):shapes.append(tag)
            pos=bisect_right(prompt_times,p.get("started_at_ms") or 0)
            cwd=recorded_path(item.get("cwd") or meta["repo"])
            root=False if cwd.startswith(("//","\\\\")) else Path(cwd).joinpath(".git").exists()
            by_id[meta["key"]+"-L"+str(n)]={"last_user_request_topics":prompts[pos-1] if pos else [],"result_shape":shapes,
                                             "cwd_is_checkout_root_at_inspection":root}
    return revision,by_id

def python_ops(text):
    bodies=re.findall(r"(?ms)@['\"]\r?\n(.*?)\r?\n['\"]@\s*\|\s*(?:python|py)\b",text)
    bodies+=re.findall(r'''(?i)\b(?:python[\d.]*|py)\s+-c\s+"([^"]+)"''',text)
    out=[]
    for body in bodies:
        try:tree=ast.parse(body)
        except (ValueError,SyntaxError):continue
        for x in ast.walk(tree):
            if not isinstance(x,ast.Call):continue
            fn=x.func.attr if isinstance(x.func,ast.Attribute) else x.func.id if isinstance(x.func,ast.Name) else ""
            if fn in {"read_text","read_bytes","write_text","write_bytes","glob","rglob","iterdir","unlink","rename","mkdir","open","execute","run","check_output"}:
                out.append(fn)
    return sorted(set(out))

def features(rows,meta):
    revision,trace=trace_signals(meta)
    catalog=Catalog(Path(meta["repo"]),revision,rows[0]["windows"])
    syntax=powershell(rows)
    answer={}
    for row in rows:
        parsed=syntax[row["id"]]
        commands=parsed["commands"]
        op,paths,_=operations(row)
        paths=list(paths)
        if op=="patch":
            paths.extend(ch["to"] for ch in row["changes"] if ch.get("to"))
        direct={c["text"]:c["fileArgs"] for c in commands if c["hasPaths"] and not c["redirects"]}
        if commands and commands[0]["text"] in direct and op in {"get-content","get-childitem","cat","ls","dir"}:
            paths=direct[commands[0]["text"]]
        # Only emit path-shaped command literals. Search patterns and stdout do not prove access.
        if not paths:
            for c in commands:
                for s in c["literals"]:
                    if isinstance(s,str) and PATH.fullmatch(s) and len(s)<140:
                        paths.append(s)
            if not paths:
                paths.extend(m.group(1) for m in PATH.finditer(row["command"]) if kind(m.group(1)))
        candidates=[f for p in dict.fromkeys(paths) if (f:=catalog.fact(row,p))][:12]
        heads=[re.split(r"[/\\]",n["name"])[-1].lower() for n in commands]
        verbs=[n for n in dict.fromkeys(heads) if re.fullmatch(r"[\w.-]{1,48}",n)]
        answer[row["id"]]={"project":catalog.profile,"file_evidence":candidates,
           "syntax":{"command_heads":verbs[:18],"powershell_parse_complete":parsed["parsed"],"literal_python_calls":python_ops(row["command"])},
           "_statements":parsed["statements"], "_direct":direct, **trace[row["id"]]}
    return answer
