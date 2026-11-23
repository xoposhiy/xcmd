"""Compare both context passes using aggregate counts only."""
from statistics import mean
from score import BASE, archived, load_json, intersect_over_union, COMMON_TARGETS, ACTIONS, pair
import hashlib,json

def main():
    receipt=load_json(BASE/"context-receipts.json")
    if receipt["protocol"]!=hashlib.sha256((BASE/"context-protocol.md").read_bytes()).hexdigest():
        raise ValueError("Context protocol changed")
    a,b=[archived(BASE/"paired-reviews.zip",k+".json",load_json(BASE/"receipts.json")["paired"][k]) for k in ("a2","b2")]
    new={k:archived(BASE/"context-reviews.zip",k+".json",v) for k,v in receipt["paired"].items()}
    ids=set(new["c_short"])
    if len(ids)!=32 or not all(set(d)==ids for d in new.values()) or not ids <=set(a) & set(b):
        raise ValueError("Context input IDs differ")
    def tags(d,k):
        s={pair(x,COMMON_TARGETS|{"project"}) for x in d[k]}
        if len(s)!=len(d[k]) or not s:raise ValueError("Invalid set")
        return s
    def match(x,y):
        return {"exact":sum(tags(x,k)==tags(y,k) for k in ids),
                "mean_jaccard":mean(intersect_over_union(tags(x,k),tags(y,k)) for k in ids)}
    result={"n":len(ids)}
    for mode in ("short","full"):
        c,d=new["c_"+mode],new["d_"+mode]
        vals=[match(reviewer,ref)["mean_jaccard"] for reviewer in (c,d) for ref in (a,b)]
        result[mode]={**match(c,d),"mean_jaccard_vs_frozen_full_reviews":mean(vals),
                      "cross_comparisons":vals,
                      "unknown_calls_c":sum(any("unknown" in p for p in tags(c,k)) for k in ids),
                      "unknown_calls_d":sum(any("unknown" in p for p in tags(d,k)) for k in ids)}
    for rev in ("c","d"):
        result["changed_"+rev]=len(ids)-match(new[rev+"_short"],new[rev+"_full"])["exact"]
    dest=BASE/"context-scores.json"
    if dest.exists() and load_json(dest)!=result:raise ValueError("Context result changed")
    dest.write_text(json.dumps(result,indent=2)+"\n",encoding="utf8")
    print(json.dumps(result,indent=2))
if __name__=="__main__":main()
