"""Pin inference code and label-free observations before scoring."""
import hashlib
import json
from pathlib import Path
from inputs import sha

BASE = Path(__file__).resolve().parent
SOURCES = ("prompt.txt","classifier.py","inputs.py","predict.py","evaluate.py","freeze.py")

def state():
    paths = [BASE/name for name in SOURCES] + [BASE/"validation"/"inputs.json"]
    files = {p.relative_to(BASE).as_posix():sha(p) for p in paths}
    digest = hashlib.sha256(json.dumps(files,sort_keys=True).encode()).hexdigest()
    return dict(sha256=digest,files=files)

if __name__ == "__main__":
    lock = BASE/"validation"/"freeze.json"
    frozen = state()
    if lock.exists() and json.loads(lock.read_text()) != frozen:
        raise SystemExit("Frozen study changed. Use a new, unseen validation set.")
    if not lock.exists():
        lock.write_text(json.dumps(frozen,indent=2)+"\n",encoding="utf8")
    print("Frozen study",frozen["sha256"])
