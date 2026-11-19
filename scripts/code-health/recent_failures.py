# @implements QualityChecks, HeadlessVerification
"""List scenario failures in the last eight completed Verify runs on main."""

import json
import re
import subprocess
import sys
import tempfile
from collections import Counter
from pathlib import Path


def command(*args, timeout=30):
    result = subprocess.run(args, capture_output=True, encoding="utf-8", errors="replace", timeout=timeout)
    if result.returncode:
        raise OSError(result.stderr.strip() or f"{args[0]} exited with code {result.returncode}")
    return result.stdout


def save(path, value):
    with tempfile.NamedTemporaryFile(mode="w", dir=path.parent, delete=False, encoding="utf-8") as temp:
        json.dump(value, temp)
    Path(temp.name).replace(path)


def failed_ids(log):
    ids = set(re.findall(r"\dZ FAIL ([\w-]+) \(\d+ ms\)", log))
    summary = re.search(r"\dZ (\d+)/(\d+) passed\. Results:", log)
    if summary and int(summary[2]) - int(summary[1]) == len(ids):
        return sorted(ids)
    return None


def read_attempt(run, attempt):
    run_id = str(run["databaseId"])
    try:
        log = command("gh", "run", "view", run_id, "--attempt", str(attempt), "--log")
    except (OSError, subprocess.TimeoutExpired):
        log = ""
    ids = failed_ids(log)
    if ids is not None:
        return ids
    # Older Verify logs only show the total. Their report contains the scenario IDs.
    if run["attempt"] != 1:
        raise ValueError("The attempt log has no complete scenario results.")
    with tempfile.TemporaryDirectory() as directory:
        command("gh", "run", "download", run_id, "-n", "code-health", "-D", directory, timeout=90)
        report = Path(directory)
        results = report / "behavior/results.json"
        if results.exists():
            scenarios = json.loads(results.read_text(encoding="utf-8-sig"))["scenarios"]
            return sorted({case["Id"] for case in scenarios if not case["passed"]})
        verification = (report / "verification.log").read_text(encoding="utf-8-sig")
        if "CHECK Behavioral scenarios" in verification:
            raise ValueError("The scenario run has no readable results.")
        return []


def recent_runs(cache):
    cache.mkdir(parents=True, exist_ok=True)
    window = cache / "runs.json"
    online = True
    try:
        runs = json.loads(command("gh", "run", "list", "--workflow", "verify.yml", "--branch", "main",
                                  "--status", "completed", "--limit", "8", "--json", "databaseId,attempt,conclusion",
                                  timeout=10))
        save(window, runs)
    except (OSError, ValueError, subprocess.TimeoutExpired) as error:
        if not window.exists():
            raise
        online = False
        print(f"Using the last saved CI run list: {error}", file=sys.stderr)
        runs = json.loads(window.read_text(encoding="utf-8"))
    return runs, online


def run_failures(cache, run, online):
    ids = set()
    complete = True
    for attempt in range(1, run["attempt"] + 1):
        if attempt == run["attempt"] and run["conclusion"] == "success":
            continue
        path = cache / f"{run['databaseId']}-{attempt}.json"
        try:
            if path.exists():
                failed = json.loads(path.read_text(encoding="utf-8"))
            elif online:
                failed = read_attempt(run, attempt)
                save(path, failed)
            else:
                raise OSError("No cached scenario results.")
            ids.update(failed)
        except (OSError, ValueError, KeyError, subprocess.TimeoutExpired) as error:
            print(f"CI history unavailable for run {run['databaseId']} attempt {attempt}: {error}", file=sys.stderr)
            complete = False
    return ids, complete


def history(cache):
    runs, online = recent_runs(cache)
    counts = Counter()
    known = 0
    for run in runs:
        ids, complete = run_failures(cache, run, online)
        counts.update(ids)
        known += complete
    print(f"Distinct scenarios with recent CI failures: {len(counts)}. Results available for {known}/{len(runs)} runs.",
          file=sys.stderr)
    return counts


def main():
    git_dir = command("git", "rev-parse", "--path-format=absolute", "--git-common-dir").strip()
    counts = history(Path(git_dir) / "xcmd-ci-history")
    for scenario, count in sorted(counts.items()):
        print(scenario if "--ids" in sys.argv else f"{scenario}\t{count}")


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, subprocess.TimeoutExpired) as error:
        sys.exit(f"Recent CI history unavailable: {error}")
