# @implements HeadlessVerification, QualityChecks, CodeHealth
"""Show verification failures on the GitHub Actions run page."""

import html
import json
import os
import re
import sys
from pathlib import Path


def read_log(path):
    return path.read_text(encoding="utf-8-sig", errors="replace") if path.is_file() else ""


def read_result(path):
    text = read_log(path)
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        # An interrupted process can leave a partial result. Its console log is still available.
        return {}


def command_value(text):
    return text.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")


def summarize(report, console, outcome, artifact_url):
    verification = read_result(report / "verification.json")
    behavior = read_result(report / "behavior/results.json")
    summary = ["## Verification checks", ""]
    annotations = []

    def failure(name, detail):
        summary.append(f"**{html.escape(name)}**\n\n<pre>{html.escape(detail)}</pre>\n")
        title = command_value(name).replace(":", "%3A").replace(",", "%2C")
        annotations.append(f"::error title={title}::{command_value(detail)}")

    if behavior:
        total, failures = behavior["total"], behavior["failures"]
        summary.append(f"Behavioral scenarios passed: **{total - failures}/{total}**.\n")
        for scenario in behavior["scenarios"]:
            if not scenario["passed"]:
                detail = scenario["failure"] or "No failure message was recorded."
                message, _, stack = detail.partition("\n   at ")
                location = next((line.strip() for line in stack.splitlines() if "Xcmd.Probe.Scenarios." in line and " in " in line), "")
                failure(scenario["Id"], "\n".join(filter(None, [message.strip()[:1000], location[:400]])))

    if outcome == "failure":
        if verification.get("status") == "failed":
            lines = read_log(report / "verification.log").splitlines()
            start = next((i for i in reversed(range(len(lines))) if lines[i].startswith("CHECK ")), None)
            name = lines[start][6:] if start is not None else "Verification"
            lines = lines[start + 1:] if start is not None else lines
            # Completed scenario failures already have individual annotations.
            if name not in ("Verification", "Behavioral scenarios") or not behavior.get("failures") or not any(
                    re.match(r"^\d+/\d+ passed\. Results:", line) for line in lines):
                failure(name, diagnostic_excerpt(lines))
        else:
            failure("Code health report", diagnostic_excerpt(read_log(console).splitlines()))
    elif outcome == "success":
        summary.append("Build, architecture, analyzers, formatting and code metrics passed.\n")
    if artifact_url:
        summary.append(f"[Download the full report, logs and test diagnostics]({artifact_url})")
    return "\n".join(summary) + "\n", annotations


def diagnostic_excerpt(lines):
    lines = [line[:500] for line in lines if line.strip()]
    if len(lines) > 30:
        lines = lines[:12] + ["…"] + lines[-18:]
    return "\n".join(lines) or "See the verification step log."


if __name__ == "__main__":
    summary, annotations = summarize(Path(sys.argv[1]), Path(sys.argv[2]),
                                     os.environ["VERIFY_OUTCOME"], os.environ.get("DIAGNOSTICS_URL"))
    with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as output:
        output.write(summary)
    for annotation in annotations:
        print(annotation)
