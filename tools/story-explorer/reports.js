// @implements TraceStoryFiles
import {choices, element, fileHref, formatSize, link, reportHeading, reportHref, sourceLink} from './ui.js';

const storyMetrics = {implements: 'Implementation files', tests: 'Test files'};
const fileMetrics = {stories: 'Stories', epics: 'Epics'};

export function buildReportData(catalogue, stories) {
  const files = new Map(catalogue.files.map(path => [path, {
    path, bytes: catalogue.fileSizes[path], stories: new Map(), epics: new Set(),
  }]));
  const epics = new Map(catalogue.epics.map(epic => [epic.id, {epic, implements: new Set(), tests: new Set()}]));
  for (const item of stories) {
    const {story, epic} = item;
    for (const relation of Object.keys(storyMetrics)) {
      for (const {path} of story[relation]) {
        const file = files.get(path);
        if (!file.stories.has(story.id)) file.stories.set(story.id, {...item, relations: []});
        file.stories.get(story.id).relations.push(relation);
        file.epics.add(epic.id);
        epics.get(epic.id)[relation].add(path);
      }
    }
  }
  return {stories, files, epics: [...epics.values()],
    matrixFiles: catalogue.matrixFiles, matrixFileSizes: catalogue.matrixFileSizes,
    dependencyGroups: catalogue.dependencyGroups};
}

function reportTable(name, columns, sort) {
  const table = element('table', '', 'rankings');
  table.setAttribute('aria-label', name);
  const header = table.createTHead().insertRow();
  for (const [key, label] of columns) {
    const cell = element('th', label);
    cell.scope = 'col';
    if (key === sort) cell.setAttribute('aria-sort', 'descending');
    header.append(cell);
  }
  return table;
}

function ranking(name, description, columns, options, sort, report, rows, filterLabel, initialFilter) {
  const section = element('section');
  const controls = element('div', '', 'report-controls');
  const filter = element('input');
  filter.type = 'search';
  filter.placeholder = filterLabel;
  filter.setAttribute('aria-label', filterLabel);
  filter.value = initialFilter ?? '';
  const sortHref = key => reportHref(report, filter.value ? {sort: key, filter: filter.value} : {sort: key});
  const sortControls = choices('Rank by', options, sort, sortHref);
  const count = element('span', '', 'result-count');
  count.setAttribute('role', 'status');
  const table = reportTable(name, columns, sort);
  const body = table.createTBody();
  const empty = element('p', 'No matching results.', 'empty');
  function applyFilter() {
    const query = filter.value.trim().toLowerCase();
    const visible = [];
    for (const {row, searchText} of rows) {
      if (searchText.toLowerCase().includes(query)) visible.push(row);
    }
    body.replaceChildren(...visible);
    empty.hidden = Boolean(visible.length);
    table.hidden = !visible.length;
    count.textContent = `${visible.length} of ${rows.length}`;
  }
  filter.addEventListener('input', () => {
    history.replaceState(history.state, '', sortHref(sort));
    for (const [index, key] of Object.keys(options).entries()) sortControls.children[index].href = sortHref(key);
    applyFilter();
  });
  applyFilter();
  controls.append(sortControls, filter, count);
  section.append(reportHeading(name, description), controls, table, empty);
  return section;
}

function rankingRow(primary, counts, searchText, group = '') {
  const row = element('tr');
  const heading = element('th');
  heading.scope = 'row';
  heading.append(primary);
  if (group) heading.append(element('span', group, 'row-context'));
  row.append(heading, ...counts.map(value => element('td', value, 'numeric')));
  return {row, searchText};
}

function storyLink(id) {
  const anchor = link('', '#story=' + id);
  const words = id.split(/(?<=[a-z0-9])(?=[A-Z])/);
  for (const [index, word] of words.entries()) {
    if (index) anchor.append(element('wbr'));
    anchor.append(word);
  }
  return anchor;
}

export function renderStoryRankings({stories}, selected) {
  const sort = selected.sort === 'tests' ? 'tests' : 'implements';
  const rows = [...stories]
    .sort((a, b) => b.story[sort].length - a.story[sort].length || a.story.id.localeCompare(b.story.id))
    .map(({epic, story}) => rankingRow(storyLink(story.id),
      [story.implements.length, story.tests.length], `${story.id} ${epic.title} ${story.statement}`, epic.title));
  return ranking('Story rankings', 'Find stories that span many implementation or test files.',
    [['name', 'Story'], ...Object.entries(storyMetrics)], storyMetrics, sort,
    'stories', rows, 'Filter stories', selected.filter);
}

export function renderFileRankings({files}, selected) {
  const sort = selected.sort === 'epics' ? 'epics' : 'stories';
  const rows = [...files.values()]
    .sort((a, b) => b[sort].size - a[sort].size || a.path.localeCompare(b.path))
    .map(file => rankingRow(link(file.path, fileHref(file.path), 'file-path'),
      [file.stories.size, file.epics.size], file.path));
  return ranking('File rankings', 'Find files shared by many distinct stories or epics.',
    [['name', 'File'], ...Object.entries(fileMetrics)], fileMetrics, sort,
    'files', rows, 'Filter paths', selected.filter);
}

export function renderFile(file, {from}) {
  const section = element('section');
  if (!file) {
    section.append(reportHeading('File not found', 'This path is missing from the current map.'));
    return section;
  }
  const treemap = from?.startsWith('#') && new URLSearchParams(from.slice(1)).get('report') === 'treemap';
  section.append(link(treemap ? '← Treemap' : '← File rankings', treemap ? from : reportHref('files'), 'breadcrumb'), element('h2', file.path, 'file-path'),
    element('p', `${file.stories.size} stories · ${file.epics.size} epics · ${formatSize(file.bytes)}`, 'file-summary'),
    sourceLink('Open source ↗', file.path, 'document-link'));
  if (!file.stories.size) {
    section.append(element('p', 'No current stories reference this file.', 'empty'));
    return section;
  }
  const table = reportTable('Stories linked to this file', [['name', 'Story'], ['relations', 'Relations']]);
  const body = table.createTBody();
  for (const {epic, story, relations} of file.stories.values()) {
    const {row} = rankingRow(storyLink(story.id), [], '', epic.title);
    row.append(element('td', relations.map(relation => relation === 'tests' ? 'Tests' : 'Implementation').join(' · ')));
    body.append(row);
  }
  section.append(table);
  return section;
}
