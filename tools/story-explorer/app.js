// @implements BrowseStories, TraceStoryFiles
import {byId, element, link, sourceLink} from './ui.js';
import {buildReportData, renderFile, renderFileRankings, renderStoryRankings} from './reports.js';
import {renderTreemap} from './treemap.js';
import {renderCharts} from './charts.js';

let catalogue;
let stories = [];
let reportData;
const expanded = new Set();
const reports = new Map([
  ['stories', ['Story rankings', renderStoryRankings]],
  ['files', ['File rankings', renderFileRankings]],
  ['treemap', ['Treemap', renderTreemap]],
  ['charts', ['Charts', renderCharts]],
]);

function route() {
  return Object.fromEntries(new URLSearchParams(location.hash.slice(1)));
}

function matches(epic, story, query) {
  return [epic.title, epic.id, story.id, story.body, ...story.implements.map(file => file.path),
    ...story.tests.map(file => file.path)].join(' ').toLowerCase().includes(query);
}

function renderNavigation() {
  const query = byId('search').value.trim().toLowerCase();
  const selected = route();
  const navigation = byId('epics');
  navigation.replaceChildren();
  let count = 0;
  for (const epic of catalogue.epics) {
    const visible = epic.stories.filter(story => matches(epic, story, query));
    if (!visible.length) continue;
    count += visible.length;
    const group = element('details', '', 'epic');
    group.open = Boolean(query) || expanded.has(epic.id);
    group.addEventListener('toggle', () => {
      if (query) return;
      if (group.open) expanded.add(epic.id); else expanded.delete(epic.id);
    });
    const heading = element('summary');
    heading.append(element('span', String(epic.order).padStart(2, '0'), 'order'),
      element('span', epic.title, 'title'), element('span', visible.length, 'count'));
    const items = element('div', '', 'story-links');
    const overview = link('Epic overview', '#epic=' + epic.id, 'overview-link');
    if (selected.epic === epic.id && !selected.story) overview.setAttribute('aria-current', 'page');
    items.append(overview);
    for (const story of visible) {
      const item = link(story.id, '#story=' + story.id, 'story-link');
      item.title = story.statement;
      if (selected.story === story.id) item.setAttribute('aria-current', 'page');
      items.append(item);
    }
    group.append(heading, items);
    navigation.append(group);
  }
  byId('match-count').textContent = query ? `${count} found` : `${count} stories`;
  if (!count) navigation.append(element('p', 'No matching stories. Try another name or path.', 'empty'));
}

function metric(value, label, className = '') {
  const node = element('div', '', 'metric ' + className);
  node.append(element('strong', value), element('span', label));
  return node;
}

function fileSection(title, files, className) {
  const section = element('section');
  const heading = element('div', '', 'files-heading');
  heading.append(element('h3', title, className), element('span', files.length, 'count'));
  section.append(heading);
  if (!files.length) {
    const kind = className === 'tests' ? 'test' : 'implementation';
    section.append(element('p', `No ${kind} files are mapped to this story.`, 'empty'));
    return section;
  }
  const table = element('table', '', 'files');
  table.setAttribute('aria-label', className === 'tests' ? 'Test files' : 'Implementation files');
  const header = element('thead');
  const labels = element('tr');
  labels.append(element('th', 'File'), element('th', 'Line'));
  header.append(labels);
  const body = element('tbody');
  for (const file of files) {
    const row = element('tr');
    const name = element('td');
    name.append(sourceLink(file.path, file.path));
    row.append(name, element('td', file.line));
    body.append(row);
  }
  table.append(header, body);
  section.append(table);
  return section;
}

function breadcrumb(epic) {
  const node = element('div', '', 'breadcrumb');
  node.append(link(epic.title, '#epic=' + epic.id));
  return node;
}

function renderStory(story, epic) {
  const main = byId('content');
  main.append(breadcrumb(epic), element('h2', story.id), element('p', story.statement, 'statement'),
    sourceLink('Read the epic document ↗', epic.path, 'document-link'));
  const uniqueFiles = new Set([...story.implements, ...story.tests].map(file => file.path)).size;
  const metrics = element('div', '', 'metrics');
  metrics.append(metric(story.implements.length, 'Implementation files', 'implementation'),
    metric(story.tests.length, 'Test files', 'tests'), metric(uniqueFiles, 'Distinct files'));
  main.append(metrics);
  const extra = story.body.split(/\n\s*\n/).slice(1).join('\n\n').trim();
  if (extra) {
    const notes = element('details', '', 'notes');
    notes.append(element('summary', 'Story notes and implementation decisions'), element('pre', extra));
    main.append(notes);
  }
  main.append(fileSection('Implementation', story.implements, 'implementation'), fileSection('Tests', story.tests, 'tests'));
  const index = stories.findIndex(item => item.story.id === story.id);
  const navigation = element('nav', '', 'reading-nav');
  navigation.setAttribute('aria-label', 'Reading order');
  navigation.append(index > 0 ? link('← ' + stories[index - 1].story.id, '#story=' + stories[index - 1].story.id) : element('span'));
  if (index + 1 < stories.length) navigation.append(link(stories[index + 1].story.id + ' →', '#story=' + stories[index + 1].story.id));
  main.append(navigation);
}

function renderEpic(epic) {
  const main = byId('content');
  main.append(element('div', `Epic ${String(epic.order).padStart(2, '0')} · ${epic.role}`, 'eyebrow'),
    element('h2', epic.title), element('p', epic.statement, 'statement'),
    sourceLink('Read the epic document ↗', epic.path, 'document-link'));
  const metrics = element('div', '', 'metrics');
  metrics.append(metric(epic.stories.length, 'Stories in reading order'));
  main.append(metrics);
  for (const story of epic.stories) {
    const card = link('', '#story=' + story.id, 'story-card');
    const counts = element('div', '', 'hint');
    counts.append(element('span', `${story.implements.length} implementation`, 'implementation'),
      element('span', `${story.tests.length} tests`, 'tests'));
    card.append(element('strong', story.id), element('p', story.statement), counts);
    main.append(card);
  }
}

function renderSelection(moveFocus = false) {
  if (!catalogue) return;
  const selected = route();
  const main = byId('content');
  main.replaceChildren();
  const [tab, title] = selected.report || selected.file ? renderReport(main, selected) : renderCatalogue(main, selected);
  byId('workspace').dataset.view = tab === 'catalogue' ? 'catalogue' : 'report';
  byId('catalogue-sidebar').hidden = tab !== 'catalogue';
  for (const view of byId('views').querySelectorAll('a')) {
    if (view.id === 'catalogue-link' ? tab === 'catalogue' : view.hash === '#report=' + tab) {
      view.setAttribute('aria-current', 'page');
    } else {
      view.removeAttribute('aria-current');
    }
  }
  document.title = `${title} · Xcmd`;
  if (moveFocus) {
    main.focus({preventScroll: true});
    if (tab === 'catalogue' && matchMedia('(max-width: 749px)').matches) main.scrollIntoView({block: 'start'});
    else window.scrollTo(0, 0);
  }
}

function renderCatalogue(main, selected) {
  const item = stories.find(item => item.story.id === selected.story);
  const epic = item?.epic || catalogue.epics.find(epic => epic.id === selected.epic);
  if (!epic) {
    main.append(element('h2', 'Story not found'),
      element('p', 'The saved link does not match the current catalogue. Choose an epic or story from the navigation.', 'empty'));
    renderNavigation();
    return ['catalogue', 'Story Explorer'];
  }
  expanded.add(epic.id);
  if (item) renderStory(item.story, epic); else renderEpic(epic);
  byId('catalogue-link').href = item ? '#story=' + item.story.id : '#epic=' + epic.id;
  renderNavigation();
  return ['catalogue', item?.story.id || epic.title];
}

function renderReport(main, selected) {
  if (selected.file) {
    const file = reportData.files.get(selected.file);
    main.append(renderFile(file, selected));
    return ['files', file?.path || 'File not found'];
  }
  const report = reports.get(selected.report);
  if (!report) {
    main.append(element('h2', 'Report not found'), element('p', 'Choose a report from the navigation.', 'empty'));
    return [null, 'Story Explorer'];
  }
  const [title, render] = report;
  main.append(render(reportData, selected));
  return [selected.report, title];
}

async function loadCatalogue() {
  byId('reload').disabled = true;
  byId('error').hidden = true;
  byId('summary').textContent = 'Reading the catalogue…';
  try {
    const response = await fetch('/api/catalogue');
    const data = await response.json();
    if (!response.ok) throw new Error(data.error || 'Could not load the catalogue.');
    const nextStories = data.epics.flatMap(epic => epic.stories.map(story => ({epic, story})));
    reportData = buildReportData(data, nextStories);
    catalogue = data;
    stories = nextStories;
    byId('summary').textContent = `${data.counts.epics} epics · ${data.counts.stories} stories · ${data.counts.files} mapped files`;
    byId('scan-time').textContent = `${data.scannedFiles} source files scanned · ${new Date(data.generatedAt).toLocaleTimeString('en-GB')}`;
    byId('issues').hidden = !data.issues.length;
    byId('issue-summary').textContent = `${data.issues.length} mapping issues`;
    byId('issue-list').replaceChildren(...data.issues.map(issue => element('li', `${issue.path}:${issue.line} — ${issue.message}`)));
    const firstEpic = '#epic=' + data.epics[0].id;
    if (!location.hash) history.replaceState(null, '', firstEpic);
    if (byId('catalogue-link').hash === '') byId('catalogue-link').href = firstEpic;
    renderSelection();
  } catch (error) {
    byId('error').textContent = `Could not reload the map. ${error.message}${catalogue ? '\nThe previous map is still shown.' : ''}`;
    byId('error').hidden = false;
    byId('summary').textContent = catalogue ? 'Reload failed · showing previous map' : 'Catalogue unavailable';
    if (!catalogue) byId('content').replaceChildren(element('p', 'Fix the reported problem, then choose Reload map.', 'empty'));
  } finally {
    byId('reload').disabled = false;
  }
}

byId('search').addEventListener('input', () => { if (catalogue) renderNavigation(); });
byId('reload').addEventListener('click', loadCatalogue);
document.querySelector('.skip').addEventListener('click', event => {
  event.preventDefault();
  byId('content').focus();
});
document.addEventListener('click', event => {
  const anchor = event.target.closest('a[href^="#"]');
  if (!anchor?.hash || anchor.hash !== location.hash || event.defaultPrevented || event.button !== 0 ||
      event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
  event.preventDefault();
  renderSelection(true);
});
window.addEventListener('hashchange', () => renderSelection(true));
loadCatalogue();
