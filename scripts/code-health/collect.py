"""Collect source metrics and render the code health report."""

import argparse
import csv
import hashlib
import json
import re
import statistics
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from importlib.metadata import version
from pathlib import Path

import lizard


LIZARD_FIELDS = ("ccn", "cognitive", "nesting", "codeLines", "parameters")


def read_limits(root):
    lizard_limits = json.loads((root / "config/metrics.json").read_text())["lizard"]
    if set(lizard_limits) != set(LIZARD_FIELDS) or any(not isinstance(value, int) or value <= 0 for value in lizard_limits.values()):
        raise ValueError("Every Lizard limit must be a positive integer.")
    values = dict(re.findall(r"^(CA150[256]):\s*(\d+)\s*$", (root / "CodeMetricsConfig.txt").read_text(), re.MULTILINE))
    return {"lizard": lizard_limits, "roslyn": {
        "ccn": int(values["CA1502"]), "maintainability": int(values["CA1505"]), "coupling": int(values["CA1506"])
    }}


def source_files(root):
    return sorted(
        path for folder in ("src", "tests")
        for path in (root / folder).rglob("*.cs")
        if not {"bin", "obj"}.intersection(path.relative_to(root).parts)
    )


def project_order(root):
    projects = {path.resolve() for folder in ("src", "tests") for path in (root / folder).glob("*/*.csproj")}
    dependencies = {path: {(path.parent / reference.attrib["Include"]).resolve()
                           for reference in ET.parse(path).getroot().iter("ProjectReference")} for path in projects}
    remaining, ordered = set(projects), []
    while remaining:
        ready = sorted(path for path in remaining if not dependencies[path].intersection(remaining))
        if not ready:
            raise ValueError("Project dependencies contain a cycle.")
        ordered.extend(ready)
        remaining.difference_update(ready)
    return ordered


def fingerprint(root):
    paths = set(source_files(root))
    for folder in ("src", "tests"):
        for pattern in ("*.csproj", "packages*.lock.json"):
            paths.update(path for path in (root / folder).rglob(pattern)
                         if not {"bin", "obj"}.intersection(path.relative_to(root).parts))
    paths.update(root.glob("config/BannedSymbols.*.txt"))
    paths.add(root / "config/metrics.json")
    paths.update(root.glob("scripts/*.ps1"))
    for name in (".editorconfig", "Directory.Build.props", "global.json", "CodeMetricsConfig.txt", "Xcmd.slnx", "NuGet.Config"):
        paths.add(root / name)
    digest = hashlib.sha256()
    for path in sorted(paths):
        digest.update(path.relative_to(root).as_posix().encode())
        digest.update(b"\0")
        digest.update(path.read_bytes())
        digest.update(b"\0")
    return digest.hexdigest()


def summarize(files, functions, symbols, types):
    real = [function for function in functions if function["kind"] == "function"]
    methods = [symbol for symbol in symbols if symbol["kind"] == "Method"]
    return {
        "files": len(files),
        "physicalLines": sum(file["physicalLines"] for file in files),
        "codeLines": sum(file["codeLines"] for file in files),
        "functions": len(real),
        "parserBlocks": len(functions) - len(real),
        "averageCcn": round(statistics.mean(function["ccn"] for function in real), 2) if real else None,
        "totalCcn": sum(function["ccn"] for function in real),
        "maxCcn": max((function["ccn"] for function in real), default=None),
        "maxCognitive": max((function["cognitive"] for function in real), default=None),
        "maxNesting": max((function["nesting"] for function in real), default=None),
        "maxFunctionLines": max((function["codeLines"] for function in real), default=None),
        "maxParameters": max((function["parameters"] for function in real), default=None),
        "roslynMaxMethodCcn": max((method.get("ccn", 0) for method in methods), default=None),
        "roslynMinMemberMi": min((symbol["maintainability"] for symbol in symbols if "maintainability" in symbol), default=None),
        "roslynMaxMemberCoupling": max((symbol.get("coupling", 0) for symbol in symbols), default=None),
        "roslynMaxTypeCoupling": max((type_.get("coupling", 0) for type_ in types), default=None),
        "roslynTypes": len(types),
        "roslynMembers": len(symbols),
        "lizardViolations": sum(bool(function["violations"]) for function in functions),
        "nearLimitFunctions": sum(function["utilization"] >= 0.8 for function in real),
    }


def collect_lizard(root, paths, limits):
    if version("lizard") != "1.25.1":
        raise ValueError("The report requires the pinned Lizard 1.25.1.")
    files, functions, duplicates = [], [], []
    for scope in ("production", "tests"):
        selected = [path for path in paths if (path.relative_to(root).parts[0] == "src") == (scope == "production")]
        extensions = lizard.get_extensions(["cognitive", "nd", "duplicate"])
        results = list(lizard.analyze_files([str(path) for path in selected], exts=extensions))
        for result in results:
            path = Path(result.filename)
            relative = path.relative_to(root).as_posix()
            project = path.relative_to(root).parts[1]
            files.append({
                "path": relative, "project": project, "scope": scope,
                "folder": Path(relative).parent.as_posix(),
                "physicalLines": len(path.read_text(encoding="utf-8-sig").splitlines()),
                "codeLines": result.nloc,
                "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            })
            for function in result.function_list:
                metrics = {
                    "ccn": function.cyclomatic_complexity,
                    "cognitive": getattr(function, "cognitive_complexity", 0),
                    "nesting": function.max_nesting_depth,
                    "codeLines": function.nloc,
                    "parameters": function.parameter_count,
                }
                functions.append({
                    "id": f"{relative}:{function.start_line}:{len(functions)}",
                    "path": relative, "project": project, "scope": scope,
                    "line": function.start_line, "endLine": function.end_line,
                    "name": function.name, "parserName": function.name,
                    "kind": "function" if function.start_line > 0 and function.name != "*global*" else "parser-block",
                    **metrics,
                    "utilization": round(max(metrics[key] / limit for key, limit in limits.items()), 4),
                    "violations": [key for key, limit in limits.items() if metrics[key] > limit],
                })
        detector = next(extension for extension in extensions if hasattr(extension, "get_duplicates"))
        blocks = [[{"path": Path(snippet.file_name).relative_to(root).as_posix(), "line": snippet.start_line, "endLine": snippet.end_line}
                   for snippet in block] for block in detector.get_duplicates(min_duplicate_tokens=70)]
        duplicates.append({"scope": scope, "minimumTokens": 70, "blocks": blocks,
                           "rate": detector.duplicate_rate()})
    return files, functions, duplicates


def read_metrics(element):
    names = {"MaintainabilityIndex": "maintainability", "CyclomaticComplexity": "ccn",
             "ClassCoupling": "coupling", "DepthOfInheritance": "inheritance",
             "SourceLines": "sourceLines", "ExecutableLines": "executableLines"}
    return {names[metric.attrib["Name"]]: int(metric.attrib["Value"])
            for metric in element.findall("./Metrics/Metric") if metric.attrib["Name"] in names}


def collect_roslyn(root, xml_path, paths):
    allowed = {path.resolve(): path.relative_to(root).as_posix() for path in paths}
    projects, symbols, types = [], [], []
    for target in ET.parse(xml_path).getroot().findall("./Targets/Target"):
        project = Path(target.attrib["Name"]).stem
        assembly = target.find("Assembly")
        projects.append({"name": project, **read_metrics(assembly)})
        for namespace in assembly.findall("./Namespaces/Namespace"):
            for type_ in namespace.iter("NamedType"):
                identity = f"{project}:{namespace.attrib['Name']}.{type_.attrib['Name']}"
                members, declarations = [], set()
                for member in type_.findall("./Members/*"):
                    file = Path(member.attrib.get("File", "")).resolve()
                    if file not in allowed:
                        continue
                    relative = allowed[file]
                    declarations.add(relative)
                    members.append({"id": f"{identity}:{member.tag}:{len(symbols) + len(members)}",
                                    "project": project, "path": relative,
                                    "scope": "production" if relative.startswith("src/") else "tests",
                                    "typeId": identity, "type": type_.attrib["Name"],
                                    "kind": member.tag, "name": member.attrib["Name"],
                                    "line": int(member.attrib.get("Line", 0)), **read_metrics(member)})
                if declarations:
                    types.append({"id": identity, "project": project,
                                  "scope": "production" if next(iter(declarations)).startswith("src/") else "tests",
                                  "name": type_.attrib["Name"], "namespace": namespace.attrib["Name"],
                                  "paths": sorted(declarations), **read_metrics(type_)})
                    symbols.extend(members)
    return projects, symbols, types


def enrich(files, functions, projects, symbols, types):
    for function in functions:
        matches = [symbol for symbol in symbols if symbol["kind"] == "Method"
                   and symbol["path"] == function["path"] and symbol["line"] == function["line"]]
        if len(matches) == 1:
            function["name"] = matches[0]["name"]
    for file in files:
        selected_functions = [function for function in functions if function["path"] == file["path"]]
        selected_symbols = [symbol for symbol in symbols if symbol["path"] == file["path"]]
        selected_types = [type_ for type_ in types if file["path"] in type_["paths"]]
        file["summary"] = summarize([file], selected_functions, selected_symbols, selected_types)
    groups = []
    for project in projects:
        selected_files = [file for file in files if file["project"] == project["name"]]
        selected_functions = [function for function in functions if function["project"] == project["name"]]
        selected_symbols = [symbol for symbol in symbols if symbol["project"] == project["name"]]
        selected_types = [type_ for type_ in types if type_["project"] == project["name"]]
        groups.append({**project, "scope": selected_files[0]["scope"] if selected_files else "unknown",
                       "summary": summarize(selected_files, selected_functions, selected_symbols, selected_types)})
    folders = []
    folder_paths = set()
    for file in files:
        parent = Path(file["path"]).parent
        while len(parent.parts) >= 2:
            folder_paths.add(parent.as_posix())
            parent = parent.parent
    for folder in sorted(folder_paths):
        selected = [file for file in files if file["path"].startswith(folder + "/")]
        paths = {file["path"] for file in selected}
        folders.append({"path": folder, "project": selected[0]["project"], "scope": selected[0]["scope"],
                        "summary": summarize(selected, [function for function in functions if function["path"] in paths],
                                             [symbol for symbol in symbols if symbol["path"] in paths],
                                             [type_ for type_ in types if paths.intersection(type_["paths"])])})
    return groups, folders


def write_csv(path, rows, fields):
    with path.open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow({key: json.dumps(value) if isinstance(value, (list, dict)) else value for key, value in row.items()})


def assess_verification(verification, current_fingerprint):
    result = dict(verification)
    result["matchesCurrentSource"] = result.get("sourceFingerprintBefore") == result.get("sourceFingerprintAfter") == current_fingerprint
    if result.get("status") == "passed" and not result["matchesCurrentSource"]:
        result["status"] = "source-changed"
    return result


def generate(root, output):
    paths = source_files(root)
    limits = read_limits(root)
    files, functions, duplicates = collect_lizard(root, paths, limits["lizard"])
    projects, symbols, types = collect_roslyn(root, output / "roslyn.xml", paths)
    expected = {path.relative_to(root).parts[1] for path in paths}
    if {project["name"] for project in projects} != expected:
        raise ValueError("The Roslyn report must include every production and test project.")
    projects, folders = enrich(files, functions, projects, symbols, types)
    current_fingerprint = fingerprint(root)
    verification = assess_verification(json.loads((output / "verification.json").read_text(encoding="utf-8-sig")), current_fingerprint)
    historical_path = root / "docs/evidence/architecture-checks-2026-11-10.json"
    historical = json.loads(historical_path.read_text(encoding="utf-8-sig")) if historical_path.exists() else None
    report = {
        "schemaVersion": 1, "generatedAt": datetime.now(timezone.utc).isoformat(),
        "root": str(root), "sourceFingerprint": current_fingerprint,
        "tools": {"lizard": version("lizard"), "roslynMetrics": "5.6.0", "roslynWorkspace": "4.12.0",
                  "sdk": json.loads((root / "global.json").read_text())["sdk"]["version"]},
        "limits": limits,
        "verification": verification, "historicalVerification": historical,
        "projects": projects, "folders": folders, "files": files,
        "functions": functions, "symbols": symbols, "types": types, "duplicates": duplicates,
        "notes": [
            "Production metrics are enforced; test metrics are informational.",
            "Lizard parses source text. Roslyn analyzes semantic symbols. Their counts and complexity values are not interchangeable.",
            "Parser pseudo blocks are excluded from function summaries and listed separately; the lint gate still checks them.",
            "Folder and file MI values are the lowest member index, not a native folder or file maintainability score.",
            "Folder totals include descendants and can overlap. Do not add parent and child folder totals.",
            "Type coupling is not additive. A partial class is measured once across its files; folder/type views may overlap.",
            "The official Metrics 5.6.0 tool ships Roslyn Workspace 4.12.0. The pinned SDK build remains the authoritative lint result.",
            "Near a limit means at least 80% of a configured Lizard maximum; it is a review aid, not a failing rule.",
            "Passing checks does not establish test coverage percentage, correct task ownership, or complete architecture quality.",
        ],
    }
    if verification.get("behaviorResults"):
        behavior = json.loads((output / verification["behaviorResults"]).read_text(encoding="utf-8-sig"))
        report["verification"]["behavior"] = {key: behavior[key] for key in ("total", "failures", "nativeApplicationWindows")}
    serialized = json.dumps(report, ensure_ascii=False, separators=(",", ":"))
    (output / "report.json").write_text(serialized, encoding="utf-8")
    template = (Path(__file__).parent / "report.html").read_text(encoding="utf-8")
    (output / "index.html").write_text(template.replace("__REPORT_DATA__", serialized.replace("<", "\\u003c")), encoding="utf-8")
    write_csv(output / "files.csv", [{**file, **file["summary"]} for file in files],
              ["path", "project", "scope", "physicalLines", "codeLines", "functions", "maxCcn", "maxCognitive", "maxNesting", "roslynMinMemberMi", "roslynMaxMemberCoupling", "lizardViolations"])
    write_csv(output / "functions.csv", functions, ["path", "line", "endLine", "project", "scope", "kind", "name", "parserName", *LIZARD_FIELDS, "utilization", "violations"])
    write_csv(output / "symbols.csv", symbols, ["project", "path", "line", "kind", "type", "name", "ccn", "maintainability", "coupling", "sourceLines", "executableLines"])
    print(f"Code health report: {output / 'index.html'}")
    for project in projects:
        if project["scope"] == "production":
            print(project["name"], json.dumps(project["summary"]))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("fingerprint", "projects", "generate"))
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--output", type=Path)
    options = parser.parse_args()
    if options.mode == "fingerprint":
        print(fingerprint(options.root.resolve()))
    elif options.mode == "projects":
        for project in project_order(options.root.resolve()):
            print(project)
    else:
        generate(options.root.resolve(), options.output.resolve())
