"""Keep the earlier classifier and override only explicit, bounded operations."""
import ast
import re

from classifier import READ, LIST, identity, operations
from method import grounded_role

def scoped_role(row,path,evidence,observe=False):
    key=identity(row,path)
    if not key:return None
    role=grounded_role(row,path,evidence["file_evidence"])
    if not role:return None
    old_facts=row.get("path_facts",[])
    recorded=any(identity(row,f["operand"])==key and "at session start" in f["kind"] for f in old_facts)
    tracked=any(identity(row,f["operand"])==key and f["at_start"]=="tracked file" for f in evidence["file_evidence"])
    # A successful direct read supplies evidence for a newly created or external file.
    return role if tracked or recorded or observe else None

def simple(row,evidence,statement):
    direct=evidence["_direct"].get(statement)
    op,paths,args=operations(dict(row,command=statement))
    if direct is None and op in READ | LIST:
        direct=next((v for command,v in evidence["_direct"].items() if statement.startswith(command+" |")),None)
    if direct is not None and op in READ | LIST:
        paths=direct
    if op=="patch":
        if not row["changes"]:return None
        paths=[p for c in row["changes"] for p in (c["path"],c.get("to")) if p]
        if row["status"]!="completed":return None
        action="write";observed=True
    elif op in READ | LIST:
        if direct is None and op in READ:return None
        if not paths:
            if op in LIST and row["cwd"]=="/repo":
                return ("mixed","read")
            return None
        action="read"
        observed=direct is not None and op in READ and row["exit"]==0 and row["output_lines"]>0 and len(evidence["_statements"])==1
    elif op=="git" and args:
        while len(args)>=2 and args[0] in {"-C","-c","--git-dir","--work-tree"}:
            args=args[2:]
        if any(a.startswith("--output") for a in args):return None
        if args[:1] in (["status"],["diff"],["ls-files"],["ls-tree"],["log"],["show"],["rev-parse"]) or args[:2]==["worktree","list"]:
            return ("git","read")
        return None
    else:return None
    roles=[scoped_role(row,p,evidence,observed) for p in paths]
    if not roles or not all(roles):return None
    return (roles[0] if len(set(roles))==1 else "mixed",action)

def python_pair(row,evidence):
    if row["exit"]!=0:return None
    matched=re.fullmatch(r"""(?is)\s*@'\r?\n(.*?)\r?\n'@\s*\|\s*(?:python|py)(?:\s+-)?\s*""",row["command"])
    if not matched:return None
    try:tree=ast.parse(matched[1])
    except (SyntaxError,ValueError):return None
    paths={}; classes=set(); modules=set(); pairs=[]
    def static_path(node):
        if isinstance(node,ast.Name):return paths.get(node.id)
        if not isinstance(node,ast.Call) or len(node.args)!=1 or node.keywords:return None
        f=node.func
        ctor=isinstance(f,ast.Name) and f.id in classes or isinstance(f,ast.Attribute) and f.attr=="Path" and isinstance(f.value,ast.Name) and f.value.id in modules
        arg=node.args[0]
        return arg.value if ctor and isinstance(arg,ast.Constant) and isinstance(arg.value,str) else None
    for stmt in tree.body:
        if isinstance(stmt,ast.ImportFrom) and stmt.module=="pathlib" and all(a.name=="Path" for a in stmt.names):
            classes.update(a.asname or a.name for a in stmt.names);continue
        if isinstance(stmt,ast.Import) and all(a.name=="pathlib" for a in stmt.names):
            modules.update(a.asname or a.name for a in stmt.names);continue
        if not isinstance(stmt,(ast.Assign,ast.Expr)):return None
        node=stmt.value
        if isinstance(stmt,ast.Assign):
            if len(stmt.targets)!=1 or not isinstance(stmt.targets[0],ast.Name):return None
            name=stmt.targets[0].id
            if name in classes|modules|{"print"}:return None
            if p:=static_path(node):paths[name]=p;continue
            paths.pop(name,None)
        if isinstance(node,ast.Call) and isinstance(node.func,ast.Name) and node.func.id=="print" and len(node.args)==1 and not node.keywords:
            node=node.args[0]
        if not isinstance(node,ast.Call) or not isinstance(node.func,ast.Attribute):return None
        op=node.func.attr
        if op not in {"read_text","read_bytes","write_text","write_bytes"} or not (p:=static_path(node.func.value)):return None
        if not all(isinstance(v,ast.Constant) for v in [*node.args,*[kw.value for kw in node.keywords]]):return None
        role=scoped_role(row,p,evidence,True)
        if not role:return None
        pairs.append((role,"write" if op.startswith("write") else "read"))
    if not pairs:return None
    roles={p[0] for p in pairs}
    return (next(iter(roles)) if len(roles)==1 else "mixed","write" if any(p[1]=="write" for p in pairs) else "read")

def apply(rows,info,baseline):
    predictions={}
    for r in rows:
        key=r["id"];old=baseline[key];facts=info[key];pair=None
        if not old["local"]:
            pair=python_pair(r,facts) if r["tool"]=="CommandExecution" else None
            if r["tool"]=="FileChange":
                pair=simple(r,facts,r["command"])
            elif not pair and facts["syntax"]["powershell_parse_complete"]:
                statements=facts["_statements"]
                if len(statements)==1:
                    pair=simple(r,facts,statements[0])
                elif statements and r["exit"]==0:
                    hits=[simple(r,facts,s) for s in statements]
                    if all(p and p[1]=="read" for p in hits):
                        targets={p[0] for p in hits}
                        pair=(next(iter(targets)) if len(targets)==1 else "mixed","read")
        predictions[key]=dict(target=pair[0],action=pair[1],local=True) if pair else dict(old)
    return predictions
