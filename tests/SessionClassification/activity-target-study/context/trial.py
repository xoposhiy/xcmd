"""Freeze one candidate, commit predictions, then publish paired aggregate scores."""
import argparse
import hashlib
import json
import sys
import zipfile
from pathlib import Path

HERE=Path(__file__).resolve().parent
BASE=HERE.parent
sys.path.insert(0,str(BASE))
from inputs import sha
from predict import read,write,predict
from freeze import state as baseline_state
from method import run

FILES=["method.py","prepare.py","evidence.py","syntax.ps1","trial.py"]

def immutable(path,value):
    if path.exists():
        if read(path)!=value:raise ValueError("Frozen artifact differs: "+path.name)
    else:write(path,value)

def freeze(work):
    if baseline_state()!=read(BASE/"validation/freeze.json"):raise ValueError("Historical method changed")
    state={"parent":read(BASE/"validation/freeze.json")["sha256"],
           "code":{f:sha(HERE/f) for f in FILES},
           "inputs":{s:sha(work/(s+".json")) for s in ("development","extra","previous","fresh")}}
    immutable(HERE/"study/freeze.json",state)

def small(pred,key):
    return {i:{"target":p[key]["target"],"action":p[key]["action"],"local":p["local"]} for i,p in pred.items()}

def inference(work,groups):
    cache=work/"cache"
    for group in groups:
        d=read(work/(group+".json"));rows=d["rows"]
        if group=="previous":
            old=read(BASE/"validation/predictions.json")
        elif group=="development":
            old=read(work.parent/"v2/dev-pred.json")
        else:
            old=predict(rows,cache)
        new,packets=run(rows,d["evidence"],cache)
        if set(old)!=set(new):raise ValueError("Prediction IDs differ")
        both={"previous":small(old,"memory"),"candidate":small(new,"answer")}
        immutable(HERE/("study/"+group+"-predictions.json"),both)
        print(group,"predictions",len(new),"candidate model calls",len(packets),flush=True)

def seal_predictions():
    groups=["development","extra","previous","fresh"]
    immutable(HERE/"study/receipt.json",{"freeze":sha(HERE/"study/freeze.json"),
              "predictions":{g:sha(HERE/("study/"+g+"-predictions.json")) for g in groups}})

def score(reference,pred):
    keys=set(reference)
    if any(set(group)!=keys for group in pred.values()):raise ValueError("Reference IDs differ")
    measures={}
    pairs={}
    for mode,guesses in pred.items():
        t=a=0;correct=set()
        deterministic=[i for i,v in guesses.items() if v["local"]]
        for i,v in guesses.items():
            target,action=reference[i].split("/")
            t+=v["target"]==target;a+=v["action"]==action
            if v["target"]==target and v["action"]==action:correct.add(i)
        measures[mode]={"n":len(keys),"target":t,"action":a,"pair":len(correct),"local":len(deterministic),
                        "local_correct":len(correct.intersection(deterministic))}
        pairs[mode]=correct
    measures["fixed"]=len(pairs["candidate"]-pairs["previous"])
    measures["lost"]=len(pairs["previous"]-pairs["candidate"])
    return measures

def evaluate():
    folder=HERE/"study"; frozen=read(folder/"freeze.json");receipt=read(folder/"receipt.json")
    if {f:sha(HERE/f) for f in FILES}!=frozen["code"] or receipt["freeze"]!=sha(folder/"freeze.json"):
        raise ValueError("Frozen method or receipt mismatch")
    if baseline_state()!=read(BASE/"validation/freeze.json") or frozen["parent"]!=baseline_state()["sha256"]:
        raise ValueError("Baseline changed")
    stats={}
    refs=read(folder/"reference-receipts.json")
    for group,digest in receipt["predictions"].items():
        p=folder/(group+"-predictions.json")
        if sha(p)!=digest:raise ValueError("Prediction digest mismatch")
        if group in {"fresh","previous"}:
            z=folder/"fresh-reference.zip" if group=="fresh" else BASE/"validation/sealed-reference.zip"
            with zipfile.ZipFile(z) as archive: raw=archive.read("reference.json")
        else:raw=(folder/(group+"-reference.json")).read_bytes()
        if hashlib.sha256(raw).hexdigest()!=refs[group]:raise ValueError("Reference digest mismatch")
        stats[group]=score(json.loads(raw.decode("utf-8-sig")),read(p))
    immutable(folder/"scores.json",stats)
    print(json.dumps(stats,indent=2))

if __name__=="__main__":
    cli=argparse.ArgumentParser()
    cli.add_argument("stage",choices=["freeze","predict","seal","score"])
    cli.add_argument("--work",type=Path)
    cli.add_argument("--groups",nargs="+",default=["development","extra","previous","fresh"])
    opts=cli.parse_args()
    if opts.stage=="freeze":freeze(opts.work)
    elif opts.stage=="predict":
        freeze(opts.work)
        inference(opts.work,opts.groups)
    elif opts.stage=="seal":seal_predictions()
    else:evaluate()
