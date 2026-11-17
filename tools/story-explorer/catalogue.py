# @implements BrowseStories, TraceStoryFiles
"""Parse the story format and join stories to source annotations."""

import json
import re


IDENTIFIER = re.compile(r"[A-Z][A-Za-z0-9]*\Z")


def scalar(value):
    value = value.strip()
    if value.startswith('"'):
        return json.loads(value)
    if value.startswith("'") and value.endswith("'"):
        return value[1:-1].replace("''", "'")
    return value


def paragraph(text):
    return " ".join(text.strip().split("\n\n", 1)[0].split())


def story_sections(body):
    sections, fence = [""], None
    for line in body.splitlines():
        marker = re.match(r"^\s{0,3}(`{3,}|~{3,})", line)
        if marker:
            token = marker[1]
            if fence is None:
                fence = token
            elif token[0] == fence[0] and len(token) >= len(fence):
                fence = None
            sections[-1] += line + "\n"
        elif fence is None and line.startswith("## "):
            sections.extend([line[3:], ""])
        else:
            sections[-1] += line + "\n"
    return sections


def parse_epic(text, path):
    lines = text.lstrip("\ufeff").splitlines()
    if not lines or lines[0] != "---" or "---" not in lines[1:]:
        raise ValueError(f"{path}: expected epic front matter.")
    end = lines.index("---", 1)
    metadata = {}
    for line in lines[1:end]:
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        key, separator, value = line.partition(":")
        if not separator or key in metadata:
            raise ValueError(f"{path}: invalid or repeated front matter key.")
        metadata[key] = scalar(value)
    if set(metadata) != {"id", "title", "order", "role"}:
        raise ValueError(f"{path}: front matter needs id, title, order and role.")
    if not IDENTIFIER.fullmatch(metadata["id"]) or not metadata["title"] or not metadata["role"]:
        raise ValueError(f"{path}: invalid epic ID, title or role.")
    try:
        order = int(metadata["order"])
        if order < 1:
            raise ValueError()
    except (ValueError, TypeError):
        raise ValueError(f"{path}: order must be a positive integer.") from None

    body = "\n".join(lines[end + 1:]).strip()
    heading, _, body = body.partition("\n")
    if heading != "# " + metadata["title"]:
        raise ValueError(f"{path}: the main heading must match the epic title.")
    sections = story_sections(body)
    stories = []
    for index in range(1, len(sections), 2):
        story_id, content = sections[index].strip(), sections[index + 1].strip()
        if not IDENTIFIER.fullmatch(story_id):
            raise ValueError(f"{path}: invalid story ID {story_id!r}.")
        statement = paragraph(content)
        if not statement.startswith("As a ") or ", I want " not in statement or "so that " not in statement:
            raise ValueError(f"{path}: {story_id} needs a user story statement.")
        stories.append({"id": story_id, "statement": statement, "body": content,
                        "implements": [], "tests": []})
    if not stories:
        raise ValueError(f"{path}: the epic has no stories.")
    return {**metadata, "order": order, "path": path,
            "statement": paragraph(sections[0]), "stories": stories}


def parse_catalogue(documents):
    epics = sorted((parse_epic(text, path) for path, text in documents), key=lambda epic: epic["order"])
    if not epics:
        raise ValueError("No epic documents found in docs/stories.")
    identifiers, orders = set(), set()
    for epic in epics:
        if epic["order"] in orders:
            raise ValueError(f"{epic['path']}: repeated epic order {epic['order']}.")
        orders.add(epic["order"])
        for identifier in [epic["id"], *(story["id"] for story in epic["stories"])]:
            if identifier in identifiers:
                raise ValueError(f"{epic['path']}: repeated ID {identifier}.")
            identifiers.add(identifier)
    return epics


def header_comments(text):
    """Read only the leading comment block, never annotation examples in code."""
    closing = None
    for number, line in enumerate(text.lstrip("\ufeff").splitlines(), 1):
        line = line.strip()
        if not line or line.startswith(("#!", "<?xml ", "<!DOCTYPE ", "<!doctype ")):
            continue
        if closing is None:
            if line.startswith(("//", "#")):
                yield number, line[2:].strip() if line.startswith("//") else line[1:].strip()
                continue
            if line.startswith("<!--"):
                closing, line = "-->", line[4:]
            elif line.startswith("/*"):
                closing, line = "*/", line[2:]
            else:
                break
        content, separator, tail = line.partition(closing)
        yield number, content.strip().lstrip("*").strip()
        if separator:
            closing = None
            if tail.strip():
                break


def parse_annotations(text, path):
    relations, issues = [], []
    seen = set()
    for line, comment in header_comments(text):
        match = re.match(r"^@(implements|tests)\b(.*)$", comment)
        if not match:
            continue
        relation, values = match.groups()
        identifiers = [value.strip() for value in values.split(",")]
        if not all(IDENTIFIER.fullmatch(value) for value in identifiers):
            issues.append({"path": path, "line": line, "message": f"Invalid @{relation} story list."})
            continue
        for identifier in identifiers:
            if (relation, identifier) not in seen:
                relations.append({"relation": relation, "id": identifier, "line": line})
                seen.add((relation, identifier))
    return relations, issues


def map_sources(epics, sources):
    stories = {story["id"]: story for epic in epics for story in epic["stories"]}
    files, issues = [], []
    for path, text in sources:
        relations, errors = parse_annotations(text, path)
        issues.extend(errors)
        if relations:
            files.append(path)
        for entry in relations:
            if entry["id"] not in stories:
                issues.append({"path": path, "line": entry["line"],
                               "message": f"Unknown story ID: {entry['id']}."})
            else:
                stories[entry["id"]][entry["relation"]].append({"path": path, "line": entry["line"]})
    return {"epics": epics, "files": files, "issues": issues,
            "counts": {"epics": len(epics), "stories": len(stories), "files": len(files)}}
