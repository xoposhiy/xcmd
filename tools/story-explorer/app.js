// @implements BrowseStories, TraceStoryFiles
const byId = id => document.getElementById(id);
const element = (tag, value = '', className = '') => {
  const node = document.createElement(tag);
  node.textContent = value;
  node.className = className;
  return node;
};
const link = (label, href, className = '') => {
  const node = element('a', label, className);
  node.href = href;
  if (href.startsWith('#')) node.addEventListener('click', event => {
    if (href !== location.hash || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    renderSelection(true);
  });
  return node;
};
const sourceLink = (label, path, className = '') => {
  const node = link(label, '/source?path=' + encodeURIComponent(path), className);
  node.target = '_blank';
  node.rel = 'noopener';
  node.title = 'Open source text in a new tab';
  return node;
};
let catalogue;
let stories = [];
const expanded = new Set();

function route() {
  const params = new URLSearchParams(location.hash.slice(1));
  return {story: params.get('story'), epic: params.get('epic')};
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
  labels.append(element('th', 'FILE'), element('th', 'LINE'));
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
  node.append(link(`${String(epic.order).padStart(2, '0')} / ${epic.title}`, '#epic=' + epic.id),
    element('span', epic.role, 'role'));
  return node;
}

function renderStory(story, epic) {
  const main = byId('content');
  main.append(breadcrumb(epic), element('div', 'USER STORY', 'eyebrow'),
    element('h2', story.id), element('p', story.statement, 'statement'),
    sourceLink('Read the epic document ↗', epic.path, 'document-link'));
  const uniqueFiles = new Set([...story.implements, ...story.tests].map(file => file.path)).size;
  const metrics = element('div', '', 'metrics');
  metrics.append(metric(story.implements.length, 'Implementation files', 'implementation'),
    metric(story.tests.length, 'Test files', 'tests'), metric(uniqueFiles, 'Distinct files'));
  main.append(metrics, element('p', 'File counts show mapped responsibilities, not test coverage or code quality.', 'hint'));
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
  main.append(element('div', `EPIC ${String(epic.order).padStart(2, '0')} / ${epic.role}`, 'eyebrow'),
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
  const item = stories.find(item => item.story.id === selected.story);
  const epic = item?.epic || catalogue.epics.find(epic => epic.id === selected.epic);
  byId('content').replaceChildren();
  if (epic) {
    expanded.add(epic.id);
    if (item) renderStory(item.story, epic); else renderEpic(epic);
  } else {
    byId('content').append(element('h2', 'Story not found'),
      element('p', 'The saved link does not match the current catalogue. Choose an epic or story from the navigation.', 'empty'));
  }
  renderNavigation();
  document.title = `${item?.story.id || epic?.title || 'Story explorer'} · Xcmd`;
  if (moveFocus) {
    byId('content').focus({preventScroll: true});
    byId('content').scrollIntoView({block: 'start'});
  }
}

async function loadCatalogue() {
  byId('reload').disabled = true;
  byId('error').hidden = true;
  byId('summary').textContent = 'Reading the catalogue…';
  try {
    const response = await fetch('/api/catalogue');
    const data = await response.json();
    if (!response.ok) throw new Error(data.error || 'Could not load the catalogue.');
    catalogue = data;
    stories = data.epics.flatMap(epic => epic.stories.map(story => ({epic, story})));
    byId('summary').textContent = `${data.counts.epics} epics · ${data.counts.stories} stories · ${data.counts.files} mapped files`;
    byId('scan-time').textContent = `${data.scannedFiles} source files scanned · ${new Date(data.generatedAt).toLocaleTimeString('en-GB')}`;
    byId('issues').hidden = !data.issues.length;
    byId('issue-summary').textContent = `${data.issues.length} mapping issues`;
    byId('issue-list').replaceChildren(...data.issues.map(issue => element('li', `${issue.path}:${issue.line} — ${issue.message}`)));
    if (!location.hash) history.replaceState(null, '', '#epic=' + data.epics[0].id);
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
window.addEventListener('hashchange', () => renderSelection(true));
loadCatalogue();
