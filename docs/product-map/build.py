"""Resolve the reviewed catalogue, validate coverage, and build its reading views."""

import argparse
import hashlib
import json
import re
import subprocess
import sys
from collections import Counter, defaultdict
from pathlib import Path
from xml.etree import ElementTree

from catalogue import EPICS, STORIES

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
PROJECTS = {"C": "Xcmd.Core", "D": "Xcmd.Desktop", "I": "Xcmd.Infrastructure"}
REVIEWED_COMMIT = "f8d2249b80b6d45fcca64404f264b26972ee9f8b"
REVIEW_DATE = "2026-11-17"
SOURCE_URL = "https://github.com/xoposhiy/xcmd/blob/" + REVIEWED_COMMIT + "/"
UI_FAMILIES = {"Xcmd.Desktop/CommanderWindow", "Xcmd.Desktop/PanelView", "Xcmd.Desktop/FileViewerWindow"}


def source_path(short):
    project, name = short.split(":", 1)
    if Path(name).suffix not in {".cs", ".csproj", ".json", ".txt", ".svg", ".ico", ".png"}:
        name += ".cs"
    return "src/" + PROJECTS[project] + "/" + name


def collect(draft=False):
    snapshot_diff = subprocess.run(["git", "diff", "--quiet", REVIEWED_COMMIT, "--", "src"], cwd=ROOT)
    if snapshot_diff.returncode and not draft:
        raise ValueError("Source differs from the reviewed commit. Review the changes and update the snapshot before rebuilding.")
    paths = subprocess.check_output(["git", "ls-files", "--", "src"], cwd=ROOT, text=True).splitlines()
    # Include new source files as well, but exclude generated outputs by using Git's ignore rules.
    paths += subprocess.check_output(["git", "ls-files", "--others", "--exclude-standard", "--", "src"], cwd=ROOT, text=True).splitlines()
    inventory = {}
    errors = []
    for path in sorted(set(paths)):
        raw = (ROOT / path).read_bytes()
        binary = Path(path).suffix in {".ico", ".png"}
        lines = None if binary else raw.decode("utf-8-sig").splitlines()
        if path.endswith(".json"):
            json.loads(raw)
        if path.endswith((".svg", ".csproj")):
            ElementTree.fromstring(raw)
        if path.endswith(".png") and not raw.startswith(b"\x89PNG\r\n\x1a\n"):
            errors.append("Invalid PNG signature: " + path)
        if path.endswith(".ico") and not raw.startswith(b"\0\0\1\0"):
            errors.append("Invalid ICO signature: " + path)
        inventory[path] = dict(path=path, project=path.split("/")[1], kind="code" if path.endswith(".cs") else "resource",
                               bytes=len(raw), lines=len(lines) if lines is not None else None,
                               sha256=hashlib.sha256(raw).hexdigest(), _lines=lines, stories=[])
    epics = {e[0]: dict(id=e[0], title=e[1], persona=e[2], value=e[3], boundary=e[4]) for e in EPICS}
    stories = []
    ids = [s["id"] for s in STORIES]
    if len(ids) != len(set(ids)):
        errors.append("Duplicate story IDs")
    for original in STORIES:
        s = {k: v for k, v in original.items() if k != "refs"}
        s["persona"] = epics[s["epic"]]["persona"]
        s["statement"] = "As a " + s["persona"] + ", I want to " + s["want"] + " so that " + s["why"] + "."
        s["locations"] = []
        for ref in original["refs"]:
            path = source_path(ref["file"])
            if path not in inventory:
                errors.append(s["id"] + " missing file: " + path)
                continue
            info = inventory[path]
            role = ref["role"] or ("technical" if s["persona"] == "developer" else "implementation")
            if info["kind"] == "resource":
                role = "resource"
            anchor = ref["anchor"]
            matches = [i + 1 for i, line in enumerate(info["_lines"] or []) if anchor and anchor in line]
            if anchor and len(matches) != 1:
                errors.append(f"{s['id']} anchor {path} {anchor!r}: {len(matches)} matches {matches}")
                continue
            line = matches[0] if anchor else (1 if info["lines"] else None)
            item = dict(file=path, line=line, end_line=None if anchor else info["lines"],
                        anchor=anchor, role=role, note=ref["note"],
                        url=SOURCE_URL + path + (f"#L{line}" if line else ""))
            s["locations"].append(item)
            info["stories"].append(dict(id=s["id"], epic=s["epic"], persona=s["persona"], role=role, line=line))
        if not s["locations"]:
            errors.append(s["id"] + " has no resolved locations")
        for dependency in s["enabled"]:
            if dependency not in ids or not dependency.startswith("T"):
                errors.append(s["id"] + " invalid technical dependency: " + dependency)
        for related in s["related"]:
            if related not in epics or related == s["epic"]:
                errors.append(s["id"] + " invalid related epic: " + related)
        stories.append(s)
    for info in inventory.values():
        if not info["stories"]:
            errors.append("Unmapped source: " + info["path"])
        del info["_lines"]
        info.update(counts(info["stories"]))
    if errors:
        raise ValueError("\n".join(errors))
    groups = defaultdict(list)
    for info in inventory.values():
        if info["kind"] == "code":
            groups[info["project"] + "/" + Path(info["path"]).name.split(".")[0]].append(info)
    families = []
    for family, files in groups.items():
        if len(files) > 1:
            relations = [s for file in files for s in file["stories"]]
            families.append(dict(name=family, files=[f["path"] for f in files],
                                 lines=sum(f["lines"] for f in files), **counts(relations)))
    snapshot_hash = hashlib.sha256("\n".join(p + " " + f["sha256"] for p, f in inventory.items()).encode()).hexdigest()
    return dict(review_date=REVIEW_DATE, commit=REVIEWED_COMMIT, source_hash=snapshot_hash,
                epics=list(epics.values()), stories=stories, files=list(inventory.values()), families=families)


def counts(relations):
    direct = {s["id"] for s in relations if s["role"] == "implementation"}
    direct_epics = {s["epic"] for s in relations if s["role"] == "implementation"}
    return dict(direct=len(direct), direct_epics=len(direct_epics),
                routing=len({s["id"] for s in relations if s["role"] == "routing"}),
                technical=len({s["id"] for s in relations if s["role"] in {"technical", "resource"}}))


def totals(data):
    code = [f for f in data["files"] if f["kind"] == "code"]
    desktop_lines = sum(f["lines"] for f in code if f["project"] == "Xcmd.Desktop")
    ui_lines = sum(f["lines"] for f in data["families"] if f["name"] in UI_FAMILIES)
    personas = Counter(s["persona"] for s in data["stories"])
    return dict(code_files=len(code), code_lines=f"{sum(f['lines'] for f in code):,}", aux_files=len(data["files"]) - len(code),
                epics=len(data["epics"]), stories=len(data["stories"]), user_stories=personas["user"],
                user_developer_stories=personas["user-developer"], developer_stories=personas["developer"],
                user_epics=sum(e["persona"] == "user" for e in data["epics"]),
                developer_epics=sum(e["persona"] == "developer" for e in data["epics"]),
                ui_family_lines=f"{ui_lines:,}", ui_family_percent=round(100 * ui_lines / desktop_lines),
                review_date=data["review_date"], commit=data["commit"], source_hash=data["source_hash"], source_url=SOURCE_URL)


def fill(text, data, values):
    """Replace __KEY__ values and {{count|file|family NAME FIELD}} metrics computed from the catalogue."""
    for key, value in values.items():
        text = text.replace("__" + key + "__", value)
    numbers = totals(data)
    files = {f["path"]: f for f in data["files"]}
    families = {f["name"]: f for f in data["families"]}

    def metric(match):
        kind, name, field = match.group(1), match.group(2), match.group(3)
        source = numbers if kind == "count" else files.get(name) if kind == "file" else families.get(name)
        key = name if kind == "count" else field
        if source is None or key not in source:
            raise ValueError("Unknown template metric: " + match.group(0))
        return f"{source[key]:,}" if isinstance(source[key], int) else str(source[key])
    text = re.sub(r"\{\{(count|file|family) ([^ }]+)(?: ([a-z_]+))?\}\}", metric, text)

    def source_link(match):
        path, anchor = match.group(1), match.group(2)
        if path not in files:
            raise ValueError("Unknown template source: " + path)
        lines = (ROOT / path).read_text(encoding="utf-8-sig").splitlines()
        found = [i + 1 for i, line in enumerate(lines) if anchor in line]
        if len(found) != 1:
            raise ValueError(f"Template anchor {path} {anchor!r}: {len(found)} matches")
        return f"[{Path(path).stem}:{found[0]}]({SOURCE_URL}{path}#L{found[0]})"
    text = re.sub(r"\[\[(src/[^#\]]+)#([^\]]+)\]\]", source_link, text)
    if "[[src/" in text:
        raise ValueError("Unresolved template source link")
    if re.search(r"__[A-Z_]+__|\{\{", text):
        raise ValueError("Unresolved template placeholder")
    return text


def story_link(s, prefix=""):
    return f"[{s['id']}: {s['title']}]({prefix}epics/{s['epic'].lower()}.md#{s['id'].lower()})"


def outputs(data):
    result = {"catalogue.json": json.dumps(data, indent=2, ensure_ascii=False) + "\n"}
    by_id = {s["id"]: s for s in data["stories"]}
    ncode = sum(f["kind"] == "code" for f in data["files"])
    nlines = sum(f["lines"] for f in data["files"] if f["kind"] == "code")
    epic_table = ["| Epic | Persona | Stories | Value |", "| --- | --- | ---: | --- |"]
    for e in data["epics"]:
        ss = [s for s in data["stories"] if s["epic"] == e["id"]]
        epic_table.append(f"| [{e['id']}: {e['title']}](epics/{e['id'].lower()}.md) | {e['persona']} | {len(ss)} | {e['value']} |")
        lines = [f"# {e['id']}: {e['title']}", "", e["value"], "", e["boundary"], "",
                 "[Catalogue](../README.md) · [Source index](../source-index.md) · [Analysis](../analysis.md)", "",
                 "## Stories", ""]
        lines += [f"- [{s['id']}: {s['title']}](#{s['id'].lower()})" for s in ss]
        for s in ss:
            lines += ["", f"<a id=\"{s['id'].lower()}\"></a>", f"## {s['id']}: {s['title']}", "", s["statement"], "", s["behavior"], ""]
            if s["related"]:
                lines += ["Also adds value to: " + ", ".join(f"[{e2}](./{e2.lower()}.md)" for e2 in s["related"]) + ".", ""]
            for role, title in [("implementation", "Direct implementation"), ("routing", "Entry-point routing (not counted as direct implementation)"),
                                ("technical", "Technical implementation"), ("resource", "Supporting build/resource files")]:
                refs = [r for r in s["locations"] if r["role"] == role]
                if not refs:
                    continue
                lines += [f"### {title}", "", "| File and location | Responsibility |", "| --- | --- |"]
                for r in refs:
                    point = f":{r['line']}" if r["line"] else ""
                    symbol = "`" + r["anchor"].replace("|", "\\|") + "`" if r["anchor"] else f"File-level evidence, lines 1–{r['end_line']}" if r["end_line"] else "Binary asset"
                    lines.append(f"| [{r['file']}{point}]({r['url']}) — {symbol} | {r['note']} |")
                lines.append("")
            if s["enabled"]:
                lines += ["### Enabled by technical stories", ""]
                lines += ["- " + story_link(by_id[t], "../") for t in s["enabled"]]
        result[f"epics/{e['id'].lower()}.md"] = "\n".join(lines).rstrip() + "\n"
    readme = (HERE / "README-template.md").read_text(encoding="utf-8")
    result["README.md"] = fill(readme, data, {"EPIC_TABLE": "\n".join(epic_table)})
    index = ["# Source-to-story index", "", "[Catalogue](README.md) · [Analysis](analysis.md) · [Interactive map](map.html)", "",
             "Every source input appears below. Direct, routing, and technical story counts are deduplicated within a file. Resources count only as technical support. Follow a story link for its exact locations and behavior limits.", ""]
    for project in PROJECTS.values():
        index += ["## " + project, "", "| Source file | Lines | Direct stories | Direct epics | Routing | Technical | Story references |", "| --- | ---: | ---: | ---: | ---: | ---: | --- |"]
        for f in data["files"]:
            if f["project"] != project:
                continue
            relations = sorted(set((s["id"], s["role"]) for s in f["stories"]))
            links = "; ".join(f"[{id}]({ 'epics/' + by_id[id]['epic'].lower() + '.md#' + id.lower()}) ({role})" for id, role in relations)
            index.append(f"| [{f['path'].split('/', 2)[2]}]({SOURCE_URL + f['path']}) | {f['lines'] if f['lines'] is not None else 'binary'} | {f['direct']} | {f['direct_epics']} | {f['routing']} | {f['technical']} | {links} |")
    index += ["", "## Partial-class families", "", "These are existing same-name partial file groups, not a proposal to split or merge types.", "",
              "| Family | Files | Lines | Direct stories | Direct epics | Routing | Technical |", "| --- | ---: | ---: | ---: | ---: | ---: | ---: |"]
    for f in sorted(data["families"], key=lambda x: (-x["direct"], -x["lines"])):
        index.append(f"| {f['name']} | {len(f['files'])} | {f['lines']} | {f['direct']} | {f['direct_epics']} | {f['routing']} | {f['technical']} |")
    result["source-index.md"] = "\n".join(index) + "\n"
    analysis_template = HERE / "analysis-template.md"
    if analysis_template.exists():
        project_table = ["| Project | C# files | C# physical lines |", "| --- | ---: | ---: |"]
        for project in PROJECTS.values():
            files = [f for f in data["files"] if f["project"] == project and f["kind"] == "code"]
            project_table.append(f"| {project} | {len(files)} | {sum(f['lines'] for f in files):,} |")
        categories = Counter("technical only" if not f["direct"] and not f["routing"] else "product/routing only" if not f["technical"] else "mixed product and technical" for f in data["files"] if f["kind"] == "code")
        classification = "File roles in this catalogue: " + ", ".join(f"**{n} {category}**" for category, n in sorted(categories.items())) + ". These categories describe mapped responsibilities, not architecture compliance."
        hot = sorted((f for f in data["files"] if f["direct"] >= 5 or f["direct_epics"] >= 3), key=lambda f: (-f["direct"], -f["lines"]))
        hotspot_table = ["| File | Lines | Direct stories | Direct epics | Routed stories | Technical stories |", "| --- | ---: | ---: | ---: | ---: | ---: |"]
        for f in hot:
            hotspot_table.append(f"| [{Path(f['path']).name}]({SOURCE_URL + f['path']}) | {f['lines']} | {f['direct']} | {f['direct_epics']} | {f['routing']} | {f['technical']} |")
        family_table = ["| Family | Files | Lines | Direct stories | Direct epics |", "| --- | ---: | ---: | ---: | ---: |"]
        for f in sorted(data["families"], key=lambda f: (-f["direct"], -f["lines"])):
            family_table.append(f"| {f['name'].split('/')[1]} | {len(f['files'])} | {f['lines']:,} | {f['direct']} | {f['direct_epics']} |")
        routing_table = ["| File | Direct stories | Routed stories |", "| --- | ---: | ---: |"]
        for f in sorted(data["files"], key=lambda f: -f["routing"])[:3]:
            routing_table.append(f"| [{Path(f['path']).name}]({SOURCE_URL + f['path']}) | {f['direct']} | {f['routing']} |")
        result["analysis.md"] = fill(analysis_template.read_text(encoding="utf-8"), data, {
            "PROJECT_TABLE": "\n".join(project_table), "CLASSIFICATION": classification, "HOTSPOT_TABLE": "\n".join(hotspot_table),
            "FAMILY_TABLE": "\n".join(family_table), "ROUTING_TABLE": "\n".join(routing_table)})
    template = HERE / "map-template.html"
    if template.exists():
        result["map.html"] = fill(template.read_text(encoding="utf-8"), data, {"CATALOGUE_JSON": json.dumps(data, ensure_ascii=False).replace("<", "\\u003c")})
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Check generated views and source hashes without writing")
    parser.add_argument("--draft", action="store_true", help="Resolve the catalogue against the working tree without writing; use while reviewing uncommitted source")
    parser.add_argument("--output", type=Path, help="With --draft, write preview views to this directory instead of the map folder")
    args = parser.parse_args()
    try:
        data = collect(draft=args.draft)
        generated = outputs(data)
        stale = []
        if args.output and not args.draft:
            raise ValueError("--output is available only with --draft.")
        for name, content in ([] if args.draft and not args.output else generated.items()):
            target = (args.output or HERE) / name
            if args.check:
                if not target.exists() or target.read_text(encoding="utf-8") != content:
                    stale.append(name)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text(content, encoding="utf-8", newline="\n")
        if stale:
            raise ValueError("Stale generated files: " + ", ".join(stale))
        print(f"{'Drafted' if args.draft else 'Checked' if args.check else 'Built'} {len(data['epics'])} epics, {len(data['stories'])} stories, {len(data['files'])} source inputs, and {len(generated)} reading views.")
        hot = sorted((f for f in data["files"] if f["direct"] >= 5 or f["direct_epics"] >= 3), key=lambda f: (-f["direct"], -f["lines"]))
        for f in hot:
            print(f"{f['path']}: {f['direct']} direct stories, {f['direct_epics']} direct epics, {f['routing']} routed stories, {f['technical']} technical stories; {f['lines']} lines")
    except (ValueError, OSError) as error:
        print(str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
