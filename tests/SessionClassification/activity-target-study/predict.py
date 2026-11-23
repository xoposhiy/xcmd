"""Run the two frozen methods on identical label-free inputs. No reference access."""
import argparse
import hashlib
import json
import os
import re
import urllib.request
from pathlib import Path

from classifier import PROMPT, SCHEMA, UNKNOWN, SessionMemory, local, operations, packet

MODEL = "gpt-6-luna"
MAX_COST = 0.30
RATE_IN = 0.10 / 1_000_000
RATE_OUT = 0.50 / 1_000_000

def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))

def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_suffix(".tmp")
    temp.write_text(json.dumps(value, ensure_ascii=False, indent=2)+"\n",encoding="utf8")
    temp.replace(path)

def classify_luna(inputs, cache):
    request = {"model":MODEL,"reasoning":{"effort":"none"},"store":False,"max_output_tokens":250,
               "instructions":PROMPT,"input":json.dumps(inputs, ensure_ascii=False,separators=(",",":")),
               "text":{"format":{"type":"json_schema","name":"activity","strict":True,"schema":SCHEMA}}}
    body = json.dumps(request,ensure_ascii=False).encode()
    dest = cache/(hashlib.sha256(body).hexdigest()+".json")
    if dest.exists():
        return read(dest)
    ledger = cache/"usage.json"
    cost = read(ledger) if ledger.exists() else {"requests":0,"input":0,"output":0,"usd":0.0}
    reserve = (len(body)+4096)*RATE_IN + 250*RATE_OUT
    if cost["usd"] + reserve > MAX_COST:
        raise RuntimeError("Study budget reached")
    cost["requests"] += 1
    cost["usd"] += reserve
    write(ledger,cost)
    key = os.getenv("OPENAI_API_KEY")
    if not key:
        match = re.search(r'(?m)^OPENAI_API_KEY=(.+)$',(Path.cwd()/".env").read_text())
        key = match.group(1).strip().strip('"').strip("'")
    query = urllib.request.Request("https://api.openai.com/v1/responses", data=body,
             headers={"Authorization":"Bearer "+key,"Content-Type":"application/json"})
    with urllib.request.urlopen(query,timeout=75) as reply:
        raw = json.load(reply)
    usage = raw["usage"]
    n,m = usage["input_tokens"],usage["output_tokens"]
    cost["input"] += n
    cost["output"] += m
    cost["usd"] += n*RATE_IN + m*RATE_OUT - reserve
    write(ledger,cost)
    try:
        if raw["status"] != "completed":
            raise ValueError("Incomplete")
        answer = json.loads(next(c["text"] for item in raw["output"] for c in item.get("content",[]) if c.get("type")=="output_text"))
        if any(answer[k] not in SCHEMA["properties"][k]["enum"] for k in ("target","action")):
            raise ValueError("Invalid label")
    except (ValueError,KeyError,StopIteration):
        answer = dict(UNKNOWN)
    write(dest,answer)
    return answer

def predict(rows,cache):
    output = {}
    memories = {}
    for row in sorted(rows,key=lambda r:(r["session"],r["start"],int(r["id"].rsplit("L",1)[1]))):
        memory = memories.setdefault(row["session"],SessionMemory())
        memory.advance(row["start"])
        op,paths,args = operations(row)
        prior = memory.lookup(row,paths)
        decision = local(row,op,paths,args)
        if decision:
            fresh = final = decision
        else:
            fresh = classify_luna(packet(row,[]),cache)
            final = classify_luna(packet(row,prior),cache) if prior else fresh
        memory.observe(row,op,paths,final)
        output[row["id"]] = {"local":bool(decision),"prior":prior,"baseline":fresh,"memory":final}
    return output

if __name__ == "__main__":
    cli = argparse.ArgumentParser()
    for arg in ("inputs","out","cache"):
        cli.add_argument(arg,type=Path)
    opts = cli.parse_args()
    result = predict(read(opts.inputs),opts.cache)
    write(opts.out,result)
    print("Predicted",len(result),"observations.")
