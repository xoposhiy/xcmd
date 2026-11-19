// @implements TraceStoryFiles
import {buildMatrix, directoryTransitions, matrixOrder} from './matrix-order.js';
import {element, formatSize, reportHref} from './ui.js';

const fileOrders = {links: 'Shared stories', folders: 'Directory order', dependencies: 'Dependency order'};
const storyOrders = {epics: 'Epic order', links: 'Shared files'};
const directoryOrders = {transitions: 'Fewer transitions', shared: 'Shared stories', paths: 'Path order'};
const minFileHeight = 3;
const maxFileHeight = 16;
const storyWidth = 10;
const groupGap = 3;
const directoryLabelHeight = 28;

function orderControl(label, options, value) {
  const control = element('label', label, 'matrix-order');
  const select = element('select');
  for (const [key, title] of Object.entries(options)) {
    const option = element('option', title);
    option.value = key;
    select.append(option);
  }
  select.value = Object.hasOwn(options, value) ? value : Object.keys(options)[0];
  control.append(select);
  return {control, select};
}

function groupEntries(entries) {
  const groups = [];
  for (const entry of entries) {
    let group = groups.at(-1);
    if (!group || group.id !== entry.group) {
      group = {id: entry.group, name: entry.epic, entries: []};
      groups.push(group);
    }
    group.entries.push(entry);
  }
  return groups;
}

function axis(groups, sizeOf, minimumGroupSize = 0) {
  let end = 0;
  const offsets = [];
  const sizes = [];
  const gaps = [];
  const padding = [];
  const headers = groups.map(({name, entries}, index) => {
    if (index) {
      gaps.push(end);
      end += groupGap;
    }
    const start = end;
    for (const entry of entries) {
      const size = sizeOf(entry);
      offsets.push(end);
      sizes.push(size);
      end += size;
    }
    const pad = minimumGroupSize - (end - start);
    if (pad > 0) {
      padding.push({top: end, size: pad});
      end += pad;
    }
    return {name, start, size: end - start};
  });
  return {offsets, sizes, gaps, padding, headers, end};
}

function paint(canvas, rows, columns, x, y) {
  const ratio = window.devicePixelRatio;
  canvas.width = Math.round(x.end * ratio);
  canvas.height = Math.round(y.end * ratio);
  canvas.style.width = x.end + 'px';
  canvas.style.height = y.end + 'px';
  const ctx = canvas.getContext('2d');
  ctx.scale(canvas.width / x.end, canvas.height / y.end);
  ctx.fillStyle = '#ffffff';
  ctx.fillRect(0, 0, x.end, y.end);
  ctx.fillStyle = '#f3f5f7';
  for (const left of x.offsets) ctx.fillRect(left + storyWidth - 1, 0, 1, y.end);
  for (const [index, top] of y.offsets.entries()) ctx.fillRect(0, top + y.sizes[index] - 1, x.end, 1);
  for (const {top, size} of y.padding) ctx.fillRect(0, top, x.end, size);
  ctx.fillStyle = '#c8d2e0';
  for (const left of x.gaps) ctx.fillRect(left, 0, groupGap, y.end);
  for (const top of y.gaps) ctx.fillRect(0, top, x.end, groupGap);
  ctx.fillStyle = '#23834f';
  const storyPositions = new Map(columns.map(({id}, index) => [id, x.offsets[index]]));
  for (const [row, file] of rows.entries()) {
    for (const story of file.links) ctx.fillRect(storyPositions.get(story), y.offsets[row], storyWidth - 1, y.sizes[row] - 1);
  }
}

function cellIndex({offsets, sizes}, position) {
  const index = offsets.findLastIndex(offset => offset <= position);
  return index >= 0 && position < offsets[index] + sizes[index] ? index : -1;
}

function showTip(section, tip, anchor, ...content) {
  tip.replaceChildren(...content);
  tip.hidden = false;
  const bounds = section.getBoundingClientRect();
  const left = Math.max(8, Math.min(anchor.left, document.documentElement.clientWidth - tip.offsetWidth - 8));
  const top = anchor.bottom + tip.offsetHeight + 8 <= window.innerHeight
    ? anchor.bottom + 6 : Math.max(4, anchor.top - tip.offsetHeight - 6);
  tip.style.left = left - bounds.left + 'px';
  tip.style.top = top - bounds.top + 'px';
}

function groupHeaders(groups, vertical, inspect, dismiss) {
  const strip = element('div', '', vertical ? 'matrix-directories' : 'matrix-epics');
  strip.setAttribute('role', 'group');
  strip.setAttribute('aria-label', vertical ? 'Source directories' : 'Story epics');
  for (const {name: fullName, start, size} of groups) {
    const parts = fullName.split('/');
    const name = vertical ? parts.at(-1) : fullName;
    const header = element('button', '', 'matrix-group');
    header.type = 'button';
    header.setAttribute('aria-label', fullName);
    const label = element('span', name, 'matrix-group-label');
    header.append(label);
    header.style[vertical ? 'top' : 'left'] = start + 'px';
    header.style[vertical ? 'height' : 'width'] = size + 'px';
    if (vertical) header.style.setProperty('--directory-indent', Math.min(48, Math.max(0, parts.length - 2) * 13) + 'px');
    header.addEventListener('pointerenter', event => {
      if (event.pointerType !== 'touch') inspect(new DOMRect(event.clientX, event.clientY), fullName);
    });
    header.addEventListener('pointerleave', dismiss);
    header.addEventListener('focus', () => inspect(label.getBoundingClientRect(), fullName));
    header.addEventListener('click', () => inspect(label.getBoundingClientRect(), fullName));
    header.addEventListener('blur', dismiss);
    header.addEventListener('keydown', event => { if (event.key === 'Escape') dismiss(); });
    strip.append(header);
  }
  return strip;
}

function interactiveMatrix(section, viewport, tip, {rows, columns, directories}, showEpics) {
  const canvas = element('canvas');
  canvas.tabIndex = 0;
  canvas.setAttribute('role', 'img');
  canvas.setAttribute('aria-label', `Implementation matrix, ${rows.length} files by ${columns.length} stories`);
  canvas.setAttribute('aria-description', 'Use the arrow keys to inspect cells.');
  const mark = element('span', '', 'matrix-mark');
  mark.setAttribute('aria-hidden', 'true');
  const stage = element('div', '', 'matrix-stage');
  stage.append(canvas, mark);
  const frame = element('div', '', 'matrix-frame');
  const maxBytes = Math.max(1, ...rows.map(file => file.bytes));
  const fileHeight = file => Math.max(minFileHeight, Math.round(maxFileHeight * Math.sqrt(file.bytes / maxBytes)));
  const x = axis(groupEntries(columns), () => storyWidth);
  const y = directories.length ? axis(directories, fileHeight, directoryLabelHeight) : axis(groupEntries(rows), fileHeight);
  paint(canvas, rows, columns, x, y);
  if (showEpics) frame.append(groupHeaders(x.headers, false, inspectGroup, dismiss));
  if (directories.length) frame.append(groupHeaders(y.headers, true, inspectGroup, dismiss));
  frame.append(stage);
  let row = 0;
  let column = 0;

  function dismiss() {
    tip.hidden = mark.hidden = true;
  }

  function inspectGroup(anchor, name) {
    mark.hidden = true;
    showTip(section, tip, anchor, element('strong', name));
  }

  function inspect(nextRow, nextColumn, scroll = false) {
    row = nextRow;
    column = nextColumn;
    const file = rows[row];
    const story = columns[column];
    const state = file.links.has(story.id) ? 'Implements' : 'Unlinked';
    const count = file.links.size;
    mark.hidden = false;
    mark.style.left = x.offsets[column] + 'px';
    mark.style.top = y.offsets[row] + 'px';
    mark.style.width = storyWidth + 'px';
    mark.style.height = y.sizes[row] + 'px';
    if (scroll) mark.scrollIntoView({block: 'nearest', inline: 'nearest'});
    showTip(section, tip, mark.getBoundingClientRect(), element('strong', story.id), element('span', story.epic),
      element('span', file.id),
      element('span', `${state} · ${count} ${count === 1 ? 'story' : 'stories'} in this file · ${formatSize(file.bytes)}`));
  }

  function inspectPointer(event) {
    const box = canvas.getBoundingClientRect();
    const nextRow = cellIndex(y, event.clientY - box.top);
    const nextColumn = cellIndex(x, event.clientX - box.left);
    if (nextRow < 0 || nextColumn < 0) dismiss();
    else if (nextRow !== row || nextColumn !== column || mark.hidden) inspect(nextRow, nextColumn);
  }

  dismiss();
  canvas.addEventListener('pointermove', event => { if (event.pointerType !== 'touch') inspectPointer(event); });
  canvas.addEventListener('pointerup', event => { if (event.pointerType === 'touch') inspectPointer(event); });
  canvas.addEventListener('pointerleave', event => { if (event.pointerType !== 'touch') dismiss(); });
  canvas.addEventListener('focus', () => {
    if (canvas.matches(':focus-visible')) inspect(row, column);
  });
  canvas.addEventListener('blur', dismiss);
  canvas.addEventListener('keydown', event => {
    if (event.key === 'Escape') {
      dismiss();
      return;
    }
    const delta = {ArrowUp: [-1, 0], ArrowDown: [1, 0], ArrowLeft: [0, -1], ArrowRight: [0, 1]}[event.key];
    if (!Array.isArray(delta)) return;
    event.preventDefault();
    inspect(Math.min(rows.length - 1, Math.max(0, row + delta[0])),
      Math.min(columns.length - 1, Math.max(0, column + delta[1])), true);
  });
  viewport.onscroll = () => { if (!frame.contains(document.activeElement)) dismiss(); };
  return frame;
}

export function renderMatrix(data, selected) {
  const section = element('section', '', 'matrix-report');
  section.append(element('h2', 'Implementation matrix'));
  const matrix = buildMatrix(data);
  if (!matrix.files.size) {
    section.append(element('p', 'No source files found under src/.', 'empty'));
    return section;
  }
  const files = orderControl('Files', fileOrders, selected.files);
  const stories = orderControl('Stories', storyOrders, selected.stories);
  const within = orderControl('Within directories', directoryOrders, selected.within);
  const sizes = Array.from(matrix.files.values(), file => file.links.size);
  const count = element('span', `${sizes.length} files · ${matrix.columns.length} stories · ` +
    `${sizes.filter(size => size > 1).length} shared · ${sizes.filter(size => size === 1).length} single-story · ` +
    `${sizes.filter(size => size === 0).length} unmapped`, 'result-count');
  const controls = element('div', '', 'report-controls');
  const transitions = element('span', '', 'result-count matrix-transitions');
  controls.append(files.control, stories.control, within.control, count, transitions);
  const hint = element('p', 'Green cells show implementation links. Row height reflects file size. Hover or tap a cell for details, or focus the matrix and use the arrow keys.', 'hint');
  const note = element('p', 'Estimated from C# type names and project references. Dependencies come first and cycles stay together.', 'hint');
  const viewport = element('div', '', 'matrix-viewport');
  const tip = element('div', '', 'matrix-tip');
  tip.setAttribute('role', 'status');
  section.append(controls, hint, note, viewport, tip);

  function draw() {
    const fileOrder = files.select.value;
    const storyOrder = stories.select.value;
    const order = matrixOrder(matrix, data.dependencyGroups, fileOrder, storyOrder, within.select.value);
    const showDirectories = fileOrder === 'folders';
    within.control.hidden = transitions.hidden = !showDirectories;
    if (showDirectories) transitions.textContent = `${directoryTransitions(order.rows)} color transitions within directories`;
    note.hidden = fileOrder !== 'dependencies';
    viewport.replaceChildren(interactiveMatrix(section, viewport, tip, order, storyOrder === 'epics'));
  }
  controls.addEventListener('change', () => {
    history.pushState(null, '', reportHref('matrix', {files: files.select.value, stories: stories.select.value, within: within.select.value}));
    draw();
  });
  draw();
  return section;
}
