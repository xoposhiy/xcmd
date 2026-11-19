// @implements TraceStoryFiles
export function buildMatrix({stories, matrixFiles, matrixFileSizes}) {
  const files = new Map(matrixFiles.map(path => {
    const directory = path.slice(0, path.lastIndexOf('/'));
    return [path, {id: path, directory, bytes: matrixFileSizes[path], links: new Set()}];
  }));
  const columns = stories.map(({epic, story}) => {
    const links = new Set();
    for (const {path} of story.implements) {
      if (!files.has(path)) continue;
      links.add(path);
      files.get(path).links.add(story.id);
    }
    return {id: story.id, group: epic.id, epic: epic.title, links};
  });
  return {files, columns};
}

function sharedLinks(left, right) {
  let shared = 0;
  for (const id of left) {
    if (right.has(id)) shared++;
  }
  return shared;
}

function cluster(entries, ranks) {
  const anchor = entry => Math.min(...Array.from(entry.links, id => ranks.get(id)));
  const remaining = entries.map(entry => ({entry, anchor: anchor(entry)}))
    .sort((a, b) => a.anchor - b.anchor || b.entry.links.size - a.entry.links.size);
  if (!remaining.length) return [];
  const result = [remaining.shift().entry];
  while (remaining.length) {
    const previous = result.at(-1).links;
    let best = 0;
    let bestScore = -1;
    for (const [index, {entry}] of remaining.entries()) {
      const shared = sharedLinks(previous, entry.links);
      const score = shared / (previous.size + entry.links.size - shared);
      if (score > bestScore) {
        best = index;
        bestScore = score;
      }
    }
    result.push(remaining.splice(best, 1)[0].entry);
  }
  return result;
}

const positions = entries => new Map(entries.map(({id}, index) => [id, index]));

function transitions(left, right) {
  if (!left || !right) return 0;
  return left.links.size + right.links.size - 2 * sharedLinks(left.links, right.links);
}

export function directoryTransitions(rows) {
  return rows.reduce((count, row, index) => rows[index - 1]?.directory === row.directory
    ? count + transitions(rows[index - 1], row) : count, 0);
}

function reduceTransitions(paths, shared) {
  const rows = [...(directoryTransitions(paths) < directoryTransitions(shared) ? paths : shared)];
  for (let pass = 0; pass < 5; pass++) {
    let improved = false;
    for (let start = 0; start < rows.length - 1; start++) {
      for (let end = start + 1; end < rows.length; end++) {
        const before = rows[start - 1];
        const after = rows[end + 1];
        const first = rows[start];
        const last = rows[end];
        // Reversing a run changes only the transitions at its two ends.
        if (transitions(before, last) + transitions(first, after) < transitions(before, first) + transitions(last, after)) {
          rows.splice(start, end - start + 1, ...rows.slice(start, end + 1).reverse());
          improved = true;
        }
      }
    }
    if (!improved) break;
  }
  return rows;
}

function compareDirectories(left, right) {
  const a = left.split('/');
  const b = right.split('/');
  for (let i = 0; i < Math.min(a.length, b.length); i++) {
    if (a[i] !== b[i]) return a[i] < b[i] ? -1 : 1;
  }
  return a.length - b.length;
}

function orderDirectories(rows, ranks, order) {
  const groups = new Map();
  for (const file of rows) {
    const parts = file.directory.split('/');
    for (let depth = 2; depth < parts.length; depth++) {
      const parent = parts.slice(0, depth).join('/');
      if (!groups.has(parent)) groups.set(parent, []);
    }
    if (!groups.has(file.directory)) groups.set(file.directory, []);
    groups.get(file.directory).push(file);
  }
  return [...groups.keys()].sort(compareDirectories).map(name => {
    const paths = groups.get(name);
    if (order === 'paths') return {name, entries: paths};
    const shared = [...cluster(paths.filter(file => file.links.size), ranks), ...paths.filter(file => !file.links.size)];
    return {name, entries: order === 'shared' ? shared : reduceTransitions(paths, shared)};
  });
}

export function matrixOrder({files, columns}, dependencyGroups, fileOrder, storyOrder, withinDirectory) {
  const naturalRows = [...files.values()];
  let rows = naturalRows;
  let directories = [];
  if (fileOrder === 'links') {
    const ranks = positions(columns);
    const shared = cluster(rows.filter(file => file.links.size > 1), ranks);
    const single = cluster(rows.filter(file => file.links.size === 1), ranks);
    const unmapped = rows.filter(file => !file.links.size);
    rows = [shared, single, unmapped].flatMap((group, index) => group.map(file => ({...file, group: index})));
  } else if (fileOrder === 'dependencies') {
    rows = dependencyGroups.flatMap((group, index) => group.map(path => ({
      ...files.get(path), group: group.length > 1 ? index : 'acyclic',
    })));
  } else if (fileOrder === 'folders') {
    directories = orderDirectories(rows, positions(columns), withinDirectory);
    rows = directories.flatMap(group => group.entries);
  }
  if (storyOrder === 'links') {
    const clustered = cluster(columns.filter(story => story.links.size), positions(rows));
    columns = [...clustered, ...columns.filter(story => !story.links.size)].map(story => ({...story, group: null}));
  }
  return {rows, columns, directories};
}
