"""Score the literal-operand method on a newly sealed session and earlier samples."""
import argparse
import hashlib
import json
import sys
import zipfile
from pathlib import Path
from trial import BASE, HERE, immutable, read, write, score, sha, small, baseline_state
from literal import apply
from predict import predict

STUDY=HERE/"literal-study"
GROUPS=("development","extra","previous","fresh","reserve")

def frozen_code():
    sources={p:sha(HERE/p) for p in ("literal.py","literal_trial.py","trial.py","method.py","evidence.py","syntax.ps1","prepare.py")}
    return {"parent":baseline_state()["sha256"],"files":sources}

def freeze_method(work):
    immutable(STUDY/"freeze.json",{"code":frozen_code(),"input_hashes":{g:sha(work/(g+".json")) for g in GROUPS},
        "primary":"Exact target and action on the new reserve session; report all secondary samples."})

def get_baseline(work,group,rows):
    if group=="reserve":
        return small(predict(rows,work/"cache"),"memory")
    return read(HERE/("study/"+group+"-predictions.json"))["previous"]

def infer(work):
    freeze_method(work)
    for g in GROUPS:
        d=read(work/(g+".json"));old=get_baseline(work,g,d["rows"])
        both={"previous":old,"candidate":apply(d["rows"],d["evidence"],old)}
        immutable(STUDY/(g+"-predictions.json"),both)
    immutable(STUDY/"receipt.json",{"freeze":sha(STUDY/"freeze.json"),
              "predictions":{g:sha(STUDY/(g+"-predictions.json")) for g in GROUPS}})
    print("All five paired prediction sets pinned.")

def aggregate():
    receipt=read(STUDY/"receipt.json")
    if read(STUDY/"freeze.json")["code"]!=frozen_code() or receipt["freeze"]!=sha(STUDY/"freeze.json"):
        raise ValueError("Candidate freeze changed")
    refs=read(HERE/"study/reference-receipts.json");refs["reserve"]=read(STUDY/"reference-receipt.json")["sha256"]
    released={}
    for g in GROUPS:
        p=STUDY/(g+"-predictions.json")
        if sha(p)!=receipt["predictions"][g]:raise ValueError("Predictions changed")
        if g in {"development","extra"}:
            raw=(HERE/("study/"+g+"-reference.json")).read_bytes()
        else:
            path={"previous":BASE/"validation/sealed-reference.zip","fresh":HERE/"study/fresh-reference.zip",
                  "reserve":STUDY/"reserve-reference.zip"}[g]
            with zipfile.ZipFile(path) as archive:raw=archive.read("reference.json")
        if hashlib.sha256(raw).hexdigest()!=refs[g]:raise ValueError("Reference mismatch")
        released[g]=score(json.loads(raw.decode("utf-8-sig")),read(p))
    immutable(STUDY/"scores.json",released)
    print(json.dumps(released,indent=2))
if __name__=="__main__":
    cli=argparse.ArgumentParser();cli.add_argument("stage",choices=["freeze","predict","score"]);cli.add_argument("--work",type=Path)
    o=cli.parse_args()
    if o.stage=="freeze":freeze_method(o.work)
    elif o.stage=="predict":infer(o.work)
    else:aggregate()
