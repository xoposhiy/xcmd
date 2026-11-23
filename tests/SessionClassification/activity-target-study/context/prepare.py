"""Prepare label-free inputs and historical facts without opening any reference."""
import re
from pathlib import Path

from classifier import MARKERS
from inputs import export_trace
from predict import read,write
from evidence import features

def shield(row):
    def safe(s):
        s=re.sub(r"(?i)(?:[A-Z]:[/\\]|/(?:Users|home|mnt|tmp|work)/)(?:[^\s'\"\x60|;,:()\[\]<>]+[/\\])*([^\s'\"\x60|;,:()\[\]<>/\\]+)",r"/outside/\1",s)
        s=re.sub(r"[\w.+-]+@[\w.-]+\.\w{2,}","[email omitted]",s)
        return s
    changed=dict(row)
    changed["command"]=safe(row["command"])
    changed["cwd"]=safe(row["cwd"])
    changed["changes"]=[dict(c,path=safe(c["path"]),**({"to":safe(c["to"])} if c.get("to") else {})) for c in row["changes"]]
    changed["path_facts"]=[dict(f,operand=safe(f["operand"])) for f in row["path_facts"]]
    changed["excerpt"]=[s for s in row["excerpt"] if s in MARKERS]
    return changed

def build(metas,existing=None,mask=False):
    rows=[];evidence={}
    for meta in metas:
        part=[r for r in existing if r["session"]==meta["key"]] if existing is not None else export_trace(meta)
        if mask:part=[shield(r) for r in part]
        evidence.update(features(part,meta))
        rows.extend(part)
    return {"rows":rows,"evidence":evidence}
