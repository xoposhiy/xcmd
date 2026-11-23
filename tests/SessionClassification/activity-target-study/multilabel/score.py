"""Measure agreement. The scoring process releases counts, never held-out row labels."""
import hashlib
import json
import zipfile
from collections import Counter
from pathlib import Path
from statistics import mean

BASE = Path(__file__).resolve().parent
COMMON_TARGETS = {"source","tests","docs","tooling","git","tracker","unknown"}
ACTIONS = {"read","write","other","unknown"}

def load_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))

def archived(path, member, expected):
    with zipfile.ZipFile(path) as archive:
        data = archive.read(member)
    if hashlib.sha256(data).hexdigest() != expected:
        raise ValueError("Input digest differs from the review receipt")
    return json.loads(data.decode("utf-8-sig"))

def pair(value, targets):
    parts = value.split("/")
    if len(parts) != 2 or parts[0] not in targets or parts[1] not in ACTIONS:
        raise ValueError("Unexpected annotation vocabulary")
    return tuple(parts)

def intersect_over_union(a, b):
    return len(a & b) / len(a | b)

def collapse(tags):
    targets = {"mixed" if t=="project" else t for t,_ in tags}
    actions = {a for _,a in tags}
    if len(actions)>1 and "write" not in actions:
        return None
    return (next(iter(targets)) if len(targets)==1 else "mixed",
            "write" if "write" in actions else next(iter(actions)))

def measure(keys, a1,b1,a2,b2):
    if not keys:
        return {"n":0}
    counts = Counter()
    overlaps, compound, known = [], [], []
    for key in keys:
        one,two = (pair(d[key],COMMON_TARGETS|{"mixed"}) for d in (a1,b1))
        left,right = ({pair(v,COMMON_TARGETS|{"project"}) for v in d[key]} for d in (a2,b2))
        if not left or not right or len(left)!=len(a2[key]) or len(right)!=len(b2[key]):
            raise ValueError("Both reviewers must provide a nonempty set of distinct pairs")
        counts["single_target"] += one[0]==two[0]
        counts["single_action"] += one[1]==two[1]
        counts["single_exact"] += one==two
        counts["multi_exact"] += left==right
        counts["multi_target_exact"] += {t for t,_ in left}=={t for t,_ in right}
        counts["multi_action_exact"] += {a for _,a in left}=={a for _,a in right}
        counts["gained_exact"] += one!=two and left==right
        counts["lost_exact"] += one==two and left!=right
        counts["a_pairs"]+=len(left)
        counts["b_pairs"]+=len(right)
        counts["a_multi"]+=len(left)>1
        counts["b_multi"]+=len(right)>1
        counts["both_multi"]+=len(left)>1 and len(right)>1
        counts["both_multiple_targets"]+=all(len({t for t,_ in side})>1 for side in (left,right))
        counts["both_multiple_actions"]+=all(len({a for _,a in side})>1 for side in (left,right))
        counts["partial_overlap"]+=bool(left&right) and left!=right
        counts["disjoint"]+=not bool(left&right)
        counts["intersections"]+=len(left&right)
        j=intersect_over_union(left,right)
        overlaps.append(j)
        if len(left)>1 or len(right)>1:
            compound.append(j)
        la={p for p in left if "unknown" not in p}
        rb={p for p in right if "unknown" not in p}
        counts["a_unknown"]+=la!=left
        counts["b_unknown"]+=rb!=right
        if la or rb:
            known.append(intersect_over_union(la,rb))
        x,y=collapse(left),collapse(right)
        if x is not None and y is not None:
            counts["collapse_eligible"]+=1
            counts["collapse_exact"]+=x==y
            counts["collapse_hidden_diff"]+=x==y and left!=right
    return {"n":len(keys),**counts,"mean_jaccard":mean(overlaps),
            "micro_f1":2*counts["intersections"]/(counts["a_pairs"]+counts["b_pairs"]),
            "multi_subset_n":len(compound),"multi_subset_jaccard":mean(compound) if compound else None,
            "known_union_n":len(known),"known_only_jaccard":mean(known) if known else None}

def model_scores(ids,pred,reference):
    if not ids:return {"n":0}
    truth=guess=hits=exact=invalid=0
    per_call=[]
    for k in ids:
        gold=set(reference[k])
        item=pred[k]
        answer=set(item["pairs"]) if item["valid"] else set()
        invalid+=not item["valid"]
        truth+=len(gold);guess+=len(answer);hits+=len(gold&answer);exact+=gold==answer
        per_call.append(intersect_over_union(gold,answer))
    return {"n":len(ids),"exact":exact,"invalid":invalid,"mean_jaccard":mean(per_call),
            "hits":hits,"predicted_pairs":guess,"reference_pairs":truth,
            "micro_precision":hits/guess if guess else None,"micro_recall":hits/truth,
            "micro_f1":2*hits/(truth+guess)}

def main():
    receipts=load_json(BASE/"receipts.json")
    if hashlib.sha256((BASE/"protocol.md").read_bytes()).hexdigest()!=receipts["protocol"]:
        raise ValueError("Protocol changed")
    new=[archived(BASE/"paired-reviews.zip",k+".json",receipts["paired"][k])
         for k in ("a1","b1","a2","b2")]
    historical=BASE.parent/"context"
    old=[archived(historical/"study/fresh-reference.zip","reference.json",
                  load_json(historical/"study/reference-receipts.json")["fresh"]),
         archived(historical/"literal-study/second-review.zip","reference.json",
                  load_json(historical/"literal-study/second-review-receipt.json")["sha256"])]
    ids=set(new[0])
    if any(set(d)!=ids for d in new) or set(old[0])!=set(old[1]):
        raise ValueError("Reviewer IDs differ")
    blind=set(old[0])
    if len(blind)!=32 or not blind <= ids or len(ids-blind)!=8:
        raise ValueError("Sample size differs from the protocol")
    conflicted={k for k in blind if old[0][k]!=old[1][k]}
    report={name:measure(sorted(keys),*new) for name,keys in [
           ("full_session",blind),("historical_conflicts",conflicted),
           ("historical_agreements",blind-conflicted),("development_challenge",ids-blind)]}
    receipt=load_json(BASE/"luna-receipt.json")
    if hashlib.sha256((BASE/"luna-prompt.txt").read_bytes()).hexdigest()!=receipt["prompt_sha256"]:
        raise ValueError("Frozen Luna prompt changed")
    pred=archived(BASE/"luna-predictions.zip","predictions.json",receipt["prediction_sha256"])
    if set(pred)!=blind:raise ValueError("Luna sample differs")
    same={k for k in blind if set(new[2][k])==set(new[3][k])}
    report["luna"]={"against_a":model_scores(blind,pred,new[2]),"against_b":model_scores(blind,pred,new[3]),
                    "agreed_calls":model_scores(same,pred,new[2])}
    dest=BASE/"scores.json"
    if dest.exists() and load_json(dest)!=report:
        raise ValueError("Published score changed")
    dest.write_text(json.dumps(report,indent=2)+"\n",encoding="utf8")
    print(json.dumps(report,indent=2))
if __name__=="__main__":
    main()
