# @tests InspectAgentSession, IntegrationVerification
"""Measure the Xcmd session inspector against the six-session classification dataset."""
import argparse
import json
import os
import subprocess
import sys
from pathlib import Path
from tempfile import TemporaryDirectory

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
STUDY = HERE / "activity-target-study"
sys.path[:0] = [str(STUDY / "terra-luna"), str(STUDY), str(STUDY / "resources")]
import study
from resources import score as resources


def measure(manifest, probe, live):
    predictions = {}
    complete = True
    mode = "--evaluate-tool-classification-live" if live else "--read-classification-fixture"
    with TemporaryDirectory(prefix="xcmd-classification-") as cache:
        for name, meta in manifest.items():
            trace = HERE / "transcripts" / (name + ".jsonl")
            run = subprocess.run(["dotnet", probe, mode, trace, meta["project"], Path(cache) / name],
                                 stdout=subprocess.PIPE, encoding="utf8")
            rows = [json.loads(line) for line in run.stdout.splitlines()]
            ids = study.record_ids(name, meta)
            for row in rows:
                key = f"{name}-L{row['id']}"
                if key in predictions:
                    raise ValueError(f"Duplicate tool call {key}")
                predictions[key] = row
            if set(ids) != {f"{name}-L{row['id']}" for row in rows}:
                raise ValueError(f"Xcmd read a different set of tool calls for {name}")
            complete &= run.returncode == 0
            if not complete:
                break
    return predictions, complete


def scores(ids, references, expected, predictions):
    report = {name: study.compare(ids, reference, predictions) for name, reference in zip(study.REVIEWERS, references)}
    predicted = {k: {"xcmd": resources.unpack(row["resources"], {})} for k, row in predictions.items()}
    report["resources"] = resources.accuracy(predicted, expected, "xcmd")
    report["resources"]["exact"] -= sum(not predictions[k]["pairs"] and not truth for k, truth in expected.items())
    report["classified"] = sum(bool(row["pairs"]) for row in predictions.values())
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--live", action="store_true", help="Send the fixture transcripts to Luna. API charges apply.")
    mode.add_argument("--check", action="store_true", help="Verify the fixture inventory and scoring offline.")
    args = parser.parse_args()
    if args.live and not os.environ.get("OPENAI_API_KEY", "").strip():
        parser.error("Set OPENAI_API_KEY before the paid run.")
    # Build before each measurement so the result follows the current checkout.
    subprocess.run(["dotnet", "build", "tests/Xcmd.Probe/Xcmd.Probe.csproj", "-c", "Release", "-r", "win-x64",
                    "-p:RestoreLockedMode=true", "--nologo", "-v", "quiet"], cwd=ROOT, check=True)
    probe = ROOT / "tests/Xcmd.Probe/bin/Release/net10.0/win-x64/Xcmd.Probe.dll"
    manifest = resources.read_json(study.SOFTWARE / "manifest.json")
    references = study.read_archive(manifest, study.REVISED / "tool-calls.zip", study.REVIEWERS, study.ACTIONS, study.TARGETS)
    objects = resources.read_rows(STUDY / "resources/resources.jsonl")
    ids = [key for name, meta in manifest.items() for key in study.record_ids(name, meta)]
    if set(ids) != objects.keys():
        raise ValueError("Resource and activity references have different call IDs")
    expected = {k: resources.unpack(row["resources"], {}) for k, row in objects.items()}
    predictions, complete = measure(manifest, probe, args.live)
    # Keep every reference call in the denominator if a request fails.
    for key in ids:
        predictions.setdefault(key, {"pairs": [], "resources": []})
    report = scores(ids, references, expected, predictions)
    if args.check:
        reference_rows = {k: {"pairs": references[0][k]["pairs"], "resources": objects[k]["resources"]} for k in ids}
        perfect = scores(ids, references, expected, reference_rows)
        if not complete or report["terra"]["mean_jaccard"] != 0 or report["resources"]["exact"] != 0:
            raise ValueError("Missing predictions must score zero")
        if perfect["terra"]["exact"] != len(ids) or perfect["resources"]["f1"] != 1:
            raise ValueError("The scorer rejected matching reference answers")
        print(f"Verified {len(ids)} tool calls in {len(manifest)} sessions, both reviews and resource labels offline.")
        return 0
    print(json.dumps(report, indent=2))
    return 0 if complete and report["classified"] == len(ids) else 1


if __name__ == "__main__":
    sys.exit(main())
