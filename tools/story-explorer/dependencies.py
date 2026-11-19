# @implements TraceStoryFiles
"""Order source files using C# type names found in the worktree."""

import posixpath
import re
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import PurePosixPath


COMMENTS_AND_STRINGS = re.compile(
    r'//[^\n]*|/\*[\s\S]*?\*/|("{3,})[\s\S]*?\1|@"(?:""|[^"])*"'
    r'|"(?:\\.|[^"\\\n])*"|\'(?:\\.|[^\'\\\n])*\''
)
IDENTIFIER = re.compile(r"\b[A-Za-z_]\w*\b")
TYPE = re.compile(
    r"\b(?P<partial>partial\s+)?(?:class|struct|interface|enum|record(?:\s+(?:class|struct))?)"
    r"\s+(?P<name>[A-Za-z_]\w*)\s*(?=[{:<;(]|\bwhere\b)"
)


def project_references(sources):
    projects = {path: set() for path in sources if path.lower().endswith(".csproj")}
    for path, references in projects.items():
        try:
            project = ET.fromstring(sources[path])
        except ET.ParseError:
            continue
        for node in project.iter():
            if node.tag.rsplit("}", 1)[-1] == "ProjectReference" and "Include" in node.attrib:
                target = node.attrib["Include"].replace("\\", "/")
                references.add(posixpath.normpath(posixpath.join(posixpath.dirname(path), target)))
        references.intersection_update(projects)
    return projects


def reachable_projects(path, projects):
    reached, pending = {path}, [path]
    while pending:
        for target in projects[pending.pop()] - reached:
            reached.add(target)
            pending.append(target)
    return reached


def type_dependencies(sources, projects):
    project_folders = {PurePosixPath(path).parent: path for path in projects}
    owners = {}
    declarations = defaultdict(dict)
    identifiers = {}
    for path, source in sources.items():
        if not path.lower().endswith(".cs"):
            continue
        owners[path] = next((project_folders[folder] for folder in PurePosixPath(path).parents
                             if folder in project_folders), None)
        code = COMMENTS_AND_STRINGS.sub(" ", source)
        for match in TYPE.finditer(code):
            declarations[match["name"]][path] = bool(match["partial"])
        identifiers[path] = set(IDENTIFIER.findall(code))

    visible = {project: reachable_projects(project, projects) | {None} for project in projects}
    references = {}
    for path, names in identifiers.items():
        links = references[path] = set()
        for name in names & declarations.keys():
            candidates = {target: partial for target, partial in declarations[name].items()
                          if owners[path] is None or owners[target] in visible[owners[path]]}
            if len(candidates) == 1 or (
                candidates and all(candidates.values()) and len({owners[target] for target in candidates}) == 1
            ):
                links.update(candidates)
        links.discard(path)
    return references


def dependency_groups(paths, sources):
    projects = project_references(sources)
    references = type_dependencies(sources, projects)
    graph = {path: sorted(references.get(path, projects.get(path, ()))) for path in paths}
    reverse = {path: [] for path in paths}
    for path, dependencies in graph.items():
        for dependency in dependencies:
            reverse[dependency].append(path)

    postorder = []
    seen = set()
    for path in graph:
        if path in seen:
            continue
        seen.add(path)
        stack = [(path, iter(graph[path]))]
        while stack:
            current, edges = stack[-1]
            target = next(edges, None)
            if target is None:
                postorder.append(current)
                stack.pop()
            elif target not in seen:
                seen.add(target)
                stack.append((target, iter(graph[target])))

    # Walking the reversed graph keeps each cycle in a single block.
    groups = []
    for path in reversed(postorder):
        if path not in seen:
            continue
        component, pending = [], [path]
        seen.remove(path)
        while pending:
            current = pending.pop()
            component.append(current)
            for target in reverse[current]:
                if target in seen:
                    seen.remove(target)
                    pending.append(target)
        groups.append(sorted(component))
    return groups[::-1]
