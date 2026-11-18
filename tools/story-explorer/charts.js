// @implements TraceStoryFiles
import {element, link, reportHeading} from './ui.js';

function pointGroups(stories) {
  const groups = new Map();
  for (const {story} of stories) {
    const x = story.implements.length;
    const y = story.tests.length;
    const key = `${x},${y}`;
    if (!groups.has(key)) groups.set(key, {x, y, stories: []});
    groups.get(key).stories.push(story);
  }
  return [...groups.values()].sort((a, b) => a.x - b.x || a.y - b.y);
}

function axis(plot, maximum, axisName) {
  const interval = Math.max(1, Math.ceil(maximum / 4));
  for (let index = 0; index <= 4; index++) {
    const tick = element('div', '', `axis-tick axis-${axisName}`);
    tick.style[axisName === 'x' ? 'left' : 'bottom'] = `${index * 25}%`;
    tick.append(element('span', index * interval));
    tick.setAttribute('aria-hidden', 'true');
    plot.append(tick);
  }
  return 4 * interval;
}

function scatter(stories) {
  const panel = element('section', '', 'chart-panel');
  panel.append(element('h3', 'Implementation vs. tests'),
    element('p', 'Select a point to see the stories with those file counts.', 'hint'));
  const points = pointGroups(stories);
  if (!points.length) {
    panel.append(element('p', 'No stories to plot.', 'empty'));
    return panel;
  }
  const graphic = element('div', '', 'scatter');
  const plot = element('div', '', 'scatter-plot');
  plot.setAttribute('role', 'group');
  plot.setAttribute('aria-label', 'Stories by number of implementation and test files');
  const xmax = axis(plot, Math.max(...points.map(point => point.x)), 'x');
  const ymax = axis(plot, Math.max(...points.map(point => point.y)), 'y');
  graphic.append(element('div', 'Test files', 'axis-title-y'), plot, element('div', 'Implementation files', 'axis-title-x'));
  const selected = element('div', '', 'point-detail');
  selected.setAttribute('aria-live', 'polite');
  let activeButton;
  function select(point, button) {
    activeButton?.setAttribute('aria-pressed', 'false');
    activeButton = button;
    button.setAttribute('aria-pressed', 'true');
    const heading = element('strong', `${point.x} implementation · ${point.y} test files`);
    const items = element('ul');
    for (const story of point.stories) {
      const item = element('li');
      item.append(link(story.id, '#story=' + story.id));
      items.append(item);
    }
    selected.replaceChildren(heading, items);
  }
  for (const point of points) {
    const count = point.stories.length;
    const label = `${point.x} implementation files, ${point.y} test files, ${count} ${count === 1 ? 'story' : 'stories'}`;
    const button = element('button', '', `scatter-point${point.y === 0 ? ' zero-tests' : ''}${count > 1 ? ' multiple' : ''}`);
    button.type = 'button';
    button.setAttribute('aria-label', label);
    button.setAttribute('aria-pressed', 'false');
    button.style.left = `${100 * point.x / xmax}%`;
    button.style.bottom = `${100 * point.y / ymax}%`;
    button.title = label;
    button.addEventListener('click', () => select(point, button));
    plot.append(button);
    if (point === points.at(-1)) select(point, button);
  }
  panel.append(graphic, selected);
  return panel;
}

function bar(count, maximum, kind, label) {
  const row = element('div', '', 'bar ' + kind);
  row.setAttribute('aria-label', `${count} ${label} files`);
  const track = element('span', '', 'bar-track');
  const fill = element('span', '', 'bar-fill');
  fill.style.width = `${100 * count / maximum}%`;
  track.append(fill);
  row.append(track, element('span', count, 'numeric'));
  return row;
}

function epicBars(epics) {
  const panel = element('section', '', 'chart-panel');
  panel.append(element('h3', 'Mapped files by epic'),
    element('p', 'Each bar counts distinct files within its epic.', 'hint'));
  const legend = element('div', '', 'bar-legend');
  legend.append(element('span', 'Implementation', 'implementation'), element('span', 'Tests', 'tests'));
  panel.append(legend);
  const maximum = Math.max(1, ...epics.flatMap(epic => [epic.implements.size, epic.tests.size]));
  const rows = element('ul', '', 'epic-bars');
  for (const {epic, implements: implementation, tests} of epics) {
    const row = element('li');
    row.append(link(epic.title, '#epic=' + epic.id), bar(implementation.size, maximum, 'implementation', 'implementation'),
      bar(tests.size, maximum, 'tests', 'test'));
    rows.append(row);
  }
  panel.append(rows);
  return panel;
}

export function renderCharts({stories, epics}) {
  const section = element('section');
  const charts = element('div', '', 'chart-grid');
  charts.append(scatter(stories), epicBars(epics));
  section.append(reportHeading('Mapping charts', 'Compare the number of mapped files across stories and epics.'), charts);
  return section;
}
