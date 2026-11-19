# @implements CodeHealth
"""Read semantic metrics for authored files and their types."""

import xml.etree.ElementTree as ET
from pathlib import Path


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
