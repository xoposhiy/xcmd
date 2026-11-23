"""Run one frozen multi-pair prompt over the already reviewed short packets."""
import hashlib
import json
import os
import re
import sys
import urllib.request
from pathlib import Path
from score import ACTIONS, BASE, COMMON_TARGETS

def dump(path,obj):
    path.write_text(json.dumps(obj,indent=2,ensure_ascii=False)+"\n",encoding="utf8")

def main(packet_file, cache):
    if hashlib.sha256(packet_file.read_bytes()).hexdigest()!="d36e38808fbd9cefe63511216c67cdb974babdc304e77445ecf54f918d07dce9":
        raise ValueError("Compact packet review receipt mismatch")
    packets=json.loads(packet_file.read_text(encoding="utf-8-sig"))
    if len(packets)!=32:raise ValueError("Wrong sample")
    cache.mkdir(exist_ok=True,parents=True)
    costs=cache/"usage.json"
    usage=json.loads(costs.read_text()) if costs.exists() else dict(usd=0,requests=0,input=0,output=0)
    schema={"type":"object","properties":{"labels":{"type":"array","items":{"type":"object","properties":{
        "pair":{"type":"string","enum":sorted(t+"/"+a for t in COMMON_TARGETS|{"project"} for a in ACTIONS)},
        "cue":{"type":"string"}},"required":["pair","cue"],"additionalProperties":False}}},
        "required":["labels"],"additionalProperties":False}
    prompt=(BASE/"luna-prompt.txt").read_text(encoding="utf8").strip()
    key=os.getenv("OPENAI_API_KEY") or re.search(r"(?m)^OPENAI_API_KEY=(.+)$",(Path.cwd()/".env").read_text()).group(1).strip().strip('"').strip("'")
    predictions={}
    for i,v in packets.items():
        packet=v["baseline"]
        body=json.dumps(dict(model="gpt-6-luna",reasoning={"effort":"none"},store=False,max_output_tokens=420,
                      instructions=prompt,input=json.dumps(packet,ensure_ascii=False,separators=(",",":")),
                      text={"format":{"type":"json_schema","name":"activity_pairs","strict":True,"schema":schema}})).encode()
        dest=cache/(hashlib.sha256(body).hexdigest()+".json")
        if dest.exists():
            predictions[i]=json.loads(dest.read_text())
            continue
        reserve=(len(body)+2048)*.1/1e6+420*.5/1e6
        if usage["usd"]+reserve>.05:raise RuntimeError("Study budget reached")
        usage["usd"]+=reserve;usage["requests"]+=1;dump(costs,usage)
        request=urllib.request.Request("https://api.openai.com/v1/responses",data=body,
                headers={"Authorization":"Bearer "+key,"Content-Type":"application/json"})
        with urllib.request.urlopen(request,timeout=60) as response:r=json.load(response)
        cost=r["usage"];n,m=cost["input_tokens"],cost["output_tokens"]
        usage["usd"]+=(n*.1+m*.5)/1e6-reserve
        usage["input"]+=n;usage["output"]+=m;dump(costs,usage)
        try:
            if r["status"]!="completed":raise ValueError("Incomplete prediction")
            out=json.loads(next(c["text"] for v in r["output"] for c in v.get("content",[]) if c.get("type")=="output_text"))["labels"]
            tags={x["pair"] for x in out}
            if not tags:raise ValueError("Empty answer")
            item={"pairs":sorted(tags),"valid":True}
        except (KeyError,ValueError,StopIteration):
            item={"pairs":[],"valid":False}
        dump(dest,item);predictions[i]=item
    out=BASE/"luna-predictions.zip"
    import zipfile
    encoded=json.dumps(predictions,sort_keys=True,indent=2).encode()
    if out.exists():
        with zipfile.ZipFile(out) as z:
            if z.read("predictions.json")!=encoded:raise ValueError("Frozen predictions differ")
    else:
        with zipfile.ZipFile(out,"x",compression=zipfile.ZIP_DEFLATED) as z:z.writestr("predictions.json",encoded)
    receipt={"packet_sha256":hashlib.sha256(packet_file.read_bytes()).hexdigest(),
             "prompt_sha256":hashlib.sha256((BASE/"luna-prompt.txt").read_bytes()).hexdigest(),
             "prediction_sha256":hashlib.sha256(encoded).hexdigest(),"usage":usage}
    dump(BASE/"luna-receipt.json",receipt)
    print("Pinned",len(predictions),"predictions; paid requests",usage["requests"],"estimated USD",round(usage["usd"],6))
if __name__=="__main__":main(Path(sys.argv[1]),Path(sys.argv[2]))
