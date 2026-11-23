"""Compare two sealed independent annotations without releasing individual labels."""
import hashlib
import json
import zipfile
from pathlib import Path

HERE=Path(__file__).resolve().parent
def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))

def reference(path,digest):
    with zipfile.ZipFile(path) as archive:raw=archive.read("reference.json")
    if hashlib.sha256(raw).hexdigest()!=digest:raise ValueError("Reference receipt mismatch")
    return json.loads(raw.decode("utf-8-sig"))

def main():
    s=HERE/"study";v=HERE/"literal-study"
    a=reference(s/"fresh-reference.zip",read(s/"reference-receipts.json")["fresh"])
    b=reference(v/"second-review.zip",read(v/"second-review-receipt.json")["sha256"])
    if a.keys()!=b.keys():raise ValueError("Reviewer ID sets differ")
    agreed={k for k in a if a[k]==b[k]}
    result={"n":len(a),"target":sum(a[k].split("/")[0]==b[k].split("/")[0] for k in a),
            "action":sum(a[k].split("/")[1]==b[k].split("/")[1] for k in a),"both":len(agreed)}
    older=read(s/"fresh-predictions.json");narrow=read(v/"fresh-predictions.json")
    for name,rows in (("earlier",older["previous"]),("context",older["candidate"]),("literal",narrow["candidate"])):
        result[name+"_pairs_on_agreed"]=sum(rows[k]["target"]+"/"+rows[k]["action"]==a[k] for k in agreed)
    dest=v/"agreement.json"
    if dest.exists() and read(dest)!=result:raise ValueError("Agreement release changed")
    dest.write_text(json.dumps(result,indent=2)+"\n",encoding="utf8")
    print(json.dumps(result))
if __name__=="__main__":main()
