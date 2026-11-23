"""Score sealed labels in an isolated process and release aggregate counts only."""
import json
import sys
import zipfile
from pathlib import Path
from classifier import TARGETS, ACTIONS
from freeze import BASE, state
from inputs import sha

def tally(ids, reference, predictions):
    answer = {}
    for arm in ("baseline","memory"):
        target = action = both = 0
        for key in ids:
            t,a = reference[key].split("/")
            if t not in TARGETS or a not in ACTIONS:
                raise ValueError("Invalid reference vocabulary")
            guess = predictions[key][arm]
            target += t == guess["target"]
            action += a == guess["action"]
            both += t == guess["target"] and a == guess["action"]
        answer[arm] = dict(target=target,action=action,both=both)
    local = sum(predictions[k]["local"] for k in ids)
    return dict(n=len(ids),local=local,model=len(ids)-local,
                supplied=sum(bool(predictions[k]["prior"]) and not predictions[k]["local"] for k in ids),
                available=sum(bool(predictions[k]["prior"]) for k in ids),
                changed=sum(any(predictions[k]["baseline"][f] != predictions[k]["memory"][f] for f in ("target","action")) for k in ids),
                **answer)

def summaries(ref,pred):
    if ref.keys() != pred.keys():
        raise ValueError("Observation IDs differ")
    groups = {"all":list(ref),"local":[k for k in ref if pred[k]["local"]],
              "model":[k for k in ref if not pred[k]["local"]]}
    for key in ref:
        groups.setdefault(key.rsplit("-L",1)[0],[]).append(key)
    return {k:tally(ids,ref,pred) for k,ids in groups.items()}

if __name__ == "__main__":
    folder = BASE/"validation"
    frozen = json.loads((folder/"freeze.json").read_text())
    if state() != frozen:
        raise SystemExit("Freeze check failed")
    manifest = json.loads((folder/"prediction-receipt.json").read_text())
    if sha(folder/"predictions.json") != manifest["predictions"] or manifest["study"] != frozen["sha256"]:
        raise SystemExit("Prediction receipt does not match")
    with zipfile.ZipFile(folder/"sealed-reference.zip") as archive:
        reference = json.loads(archive.read("reference.json").decode("utf-8-sig"))
    scores = summaries(reference,json.loads((folder/"predictions.json").read_text(encoding="utf8")))
    result = {"study":frozen["sha256"],"predictions":manifest["predictions"],
              "sealed_archive":sha(folder/"sealed-reference.zip"),"scores":scores}
    out = folder/"scores.json"
    content = json.dumps(result,indent=2)+"\n"
    if out.exists() and out.read_text() != content:
        sys.exit("Sealed score differs from its original release")
    out.write_text(content,encoding="utf8")
    print(json.dumps(scores["all"]))
