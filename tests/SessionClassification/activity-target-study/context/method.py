"""Classify explicit resources locally and use bounded context for remaining calls."""
import re
from pathlib import PurePosixPath

from classifier import FILE_ROLES, LIST, READ, SessionMemory, identity, local, operations, packet, path_role, result
from predict import classify_luna

def grounded_role(row, path, facts):
    key=identity(row,path)
    if not key:return None
    p=PurePosixPath(key.lower())
    markers=next((v.get("markers",[]) for v in facts if identity(row,v["operand"])==key),[])
    if p.name=="skill.md" or any(x in p.parts for x in (".codex",".agents",".claude")) and "skills" in p.parts:
        return "tooling"
    if p.suffix in {".md",".markdown",".rst",".adoc"}:
        return "docs"
    if p.name in {".gitignore",".gitattributes",".editorconfig","requirements.txt","pnpm-lock.yaml","tsconfig.json","uv.lock"}:
        return "tooling"
    if path_role(key)=="tooling":
        return "tooling"
    if "tests" in p.parts or "test definitions" in markers:
        return "tests"
    if "package entry point" in markers or "application startup" in markers:
        return "source"
    known=path_role(key)
    if known:return known
    # A conventional layout still identifies a literal operand in a different working copy.
    if not key.startswith("/repo/"):
        for i,segment in enumerate(p.parts):
            if segment in {"docs","scripts","tests","tools","src"}:
                return path_role("/repo/"+"/".join(p.parts[i:]))
    return None

def literal_call(row, evidence, use_memory):
    op,paths,args=operations(row)
    facts=evidence["file_evidence"]
    direct=next((v for text,v in evidence["_direct"].items() if row["command"]==text or row["command"].startswith(text+" |")),None)
    if direct is not None and op in READ | LIST:
        paths=direct
    if op in LIST and not paths and evidence["cwd_is_checkout_root_at_inspection"] and (op not in {"rg","grep"} or args):
        return result("mixed","read","Directory query at the checkout root")
    if op=="patch":
        paths=[p for c in row["changes"] for p in (c["path"],c.get("to")) if p]
    if op in READ | LIST | {"patch"} and paths:
        mapped=[grounded_role(row,p,facts) for p in paths]
        if use_memory:
            roles={v["path"]:v["target"] for v in use_memory}
            mapped=[r or roles.get(identity(row,p)) for r,p in zip(mapped,paths)]
        if all(mapped):
            target=mapped[0] if len(set(mapped))==1 else "mixed"
            action="read" if op!="patch" else "write" if row["status"]=="completed" else "other"
            return result(target,action,"Explicit operands: "+", ".join(paths[:3]))
    if op=="git":
        args=list(args)
        while len(args)>=2 and args[0] in ("-C","-c","--git-dir","--work-tree"):
            args=args[2:]
        if args and (args[:2] in (["worktree","list"],["config","--get"]) or args[0] in {"rev-list","for-each-ref","ls-remote"}):
            return result("git","read","Read Git repository state")
    if op in {"pytest","vstest.console","ctest","vitest"} or op in {"python","python3","py"} and args[:2] in (["-m","pytest"],["-m","unittest"]):
        return result("tests","other","Run test suite")
    return local(row,op,paths,args)

def decide(row, facts, history):
    statements=facts["_statements"]
    if len(statements)>1 and row["exit"]==0:
        answers=[literal_call(dict(row,command=s),facts,history) for s in statements]
        # An overall shell exit code does not prove an intermediate write succeeded.
        if all(a and a["action"]=="read" for a in answers):
            targets={a["target"] for a in answers}
            return result(next(iter(targets)) if len(targets)==1 else "mixed","read","Multiple observed queries")
    return literal_call(row,facts,history)

def make_packet(row,facts,history):
    data=packet(row,history)
    data["context_evidence"]={k:v for k,v in facts.items() if not k.startswith("_") and v}
    data["interpretation"]={
        "file_evidence":"These are command operands or path-shaped literals. A historical marker describes a file's role. A literal alone does not prove a read or write.",
        "syntax":"Recorded syntax can include unexecuted branches. Prefer an observed result. Identify the resource a script operates on.",
        "project":"Topic and file-kind counts describe the historical repository. A developer tool's own delivered code is application source.",
        "last_user_request_topics":"These are weak hints for ambiguous names. A request does not prove that an operation happened."}
    return data

def run(rows, evidence, cache, predict=True):
    guesses={}
    memories={}
    packets={}
    for row in sorted(rows,key=lambda r:(r["session"],r["start"],r["end"])):
        facts=evidence[row["id"]]
        memory=memories.setdefault(row["session"],SessionMemory())
        memory.advance(row["start"])
        op,paths,_=operations(row)
        paths=paths or [v["operand"] for v in facts["file_evidence"]]
        prior=memory.lookup(row,paths)
        decision=decide(row,facts,prior)
        if decision:
            guess=decision
        else:
            body=make_packet(row,facts,prior)
            packets[row["id"]]=body
            guess=classify_luna(body,cache) if predict else result("unknown","unknown","Pending")
            if op=="patch":
                guess=dict(guess,action="write" if row["status"]=="completed" else "other")
            elif op in READ | LIST and len(facts["_statements"])<=1:
                guess=dict(guess,action="read")
        memory.observe(row,op,paths,guess)
        guesses[row["id"]]={"answer":guess,"local":bool(decision),"used_prior":bool(prior)}
    return guesses,packets
