// @implements TraceStoryFiles
import {choices, element, fileHref, formatSize, link, reportHeading, reportHref} from './ui.js';

const heatBands = [
  {max: 0, label: '0'}, {max: .25, label: '>0–0.25'}, {max: .5, label: '>0.25–0.5'},
  {max: 1, label: '>0.5–1'}, {max: Infinity, label: '>1'},
];
const colorMetrics = {stories: 'Stories', epics: 'Epics'};
const rateFormat = new Intl.NumberFormat('en', {maximumFractionDigits: 2});
const preferredTileAspect = 1.8;

function folderNode(path, name) {
  return {path, name, bytes: 0, links: 0, children: []};
}

function addFileToFolder(folder, file) {
  folder.bytes += file.bytes;
  if (file.bytes > 0) folder.links += file.links;
}

function buildTree(files, color) {
  const root = folderNode('', 'Repository');
  const folders = new Map([['', root]]);
  for (const file of files.values()) {
    const parts = file.path.split('/');
    const name = parts.pop();
    const item = {path: file.path, name, bytes: file.bytes, links: file[color].size};
    let folder = root;
    addFileToFolder(folder, item);
    for (const part of parts) {
      const path = folder.path ? `${folder.path}/${part}` : part;
      if (!folders.has(path)) {
        const child = folderNode(path, part);
        folders.set(path, child);
        folder.children.push(child);
      }
      folder = folders.get(path);
      addFileToFolder(folder, item);
    }
    folder.children.push(item);
  }
  return folders;
}

function closestSplit(items, total) {
  let weight = items[0].bytes;
  let index = 1;
  while (index < items.length - 1) {
    const next = weight + items[index].bytes;
    if (Math.abs(total / 2 - weight) <= Math.abs(total / 2 - next)) break;
    weight = next;
    index++;
  }
  return {index, weight};
}

function partition(items, total, rect, place) {
  if (items.length < 2) {
    if (items.length) place(items[0], rect);
    return;
  }
  const {index, weight} = closestSplit(items, total);
  const fraction = weight / total;
  const {x, y, width, height} = rect;
  const splitWidth = width >= height * preferredTileAspect;
  const first = splitWidth
    ? {...rect, width: width * fraction}
    : {...rect, height: height * fraction};
  const rest = splitWidth
    ? {...rect, x: x + first.width, width: width - first.width}
    : {...rect, y: y + first.height, height: height - first.height};
  partition(items.slice(0, index), weight, first, place);
  partition(items.slice(index), total - weight, rest, place);
}

function position(node, rect, width, height) {
  node.style.left = `${100 * rect.x / width}%`;
  node.style.top = `${100 * rect.y / height}%`;
  node.style.width = `${100 * rect.width / width}%`;
  node.style.height = `${100 * rect.height / height}%`;
}

function linkDensity(item) {
  return 1024 * item.links / item.bytes;
}

function heatBand(rate) {
  return heatBands.findIndex(entry => rate <= entry.max);
}

function drawTiles(container, items, width, height, color, navigation) {
  const visible = items.filter(item => item.bytes > 0).sort((a, b) =>
    heatBand(linkDensity(b)) - heatBand(linkDensity(a)) || b.bytes - a.bytes || a.name.localeCompare(b.name));
  const total = visible.reduce((sum, item) => sum + item.bytes, 0);
  partition(visible, total, {x: 0, y: 0, width, height}, (item, rect) => {
    const tile = item.children ? folderTile(item, rect, color, navigation) : fileTile(item, color, navigation.file);
    position(tile, rect, width, height);
    container.append(tile);
  });
}

function fileTile(item, color, href) {
  const rate = linkDensity(item);
  const tile = link('', href(item.path), `map-tile map-file heat-${heatBand(rate)}`);
  const density = rate > 0 && rate < .01 ? '<0.01' : rateFormat.format(rate);
  const description = `${item.path} · ${formatSize(item.bytes)} · ${item.links} ${color} · ${density} ${color}/KiB`;
  tile.title = description;
  tile.setAttribute('aria-label', description);
  tile.append(element('span', item.name, 'tile-name'));
  return tile;
}

function folderTile(item, rect, color, navigation) {
  const header = rect.width >= 34 && rect.height >= 31 ? 21 : 0;
  const tile = element('div', '', 'map-tile map-folder');
  if (header) {
    const label = link('', navigation.folder(item.path), 'folder-label');
    const action = `Open folder ${item.path}`;
    const size = formatSize(item.bytes);
    label.setAttribute('aria-label', action);
    label.title = `${action} · ${size}`;
    label.style.height = `${header}px`;
    label.append(element('span', item.name, 'folder-name'), element('span', size, 'folder-size'));
    tile.append(label);
  }
  const inner = element('div', '', 'map-inner');
  inner.style.top = `${header}px`;
  drawTiles(inner, item.children, rect.width, rect.height - header, color, navigation);
  tile.append(inner);
  return tile;
}

function drawMap(plot, items, color, navigation) {
  const observer = new ResizeObserver(([entry]) => {
    if (!plot.isConnected) { observer.disconnect(); return; }
    const {width, height} = entry.contentRect;
    if (!width || !height) return;
    const focused = plot.contains(document.activeElement) ? document.activeElement.href : null;
    const tiles = document.createDocumentFragment();
    drawTiles(tiles, items, width, height, color, navigation);
    plot.replaceChildren(tiles);
    const clipped = [...plot.querySelectorAll('.tile-name')].filter(label => {
      const name = label.getBoundingClientRect();
      const tile = label.parentElement.getBoundingClientRect();
      return name.width > tile.width || name.height > tile.height;
    });
    for (const label of clipped) label.hidden = true;
    if (focused) {
      const match = [...plot.querySelectorAll('a')].find(anchor => anchor.href === focused);
      match?.focus({preventScroll: true});
    }
  });
  observer.observe(plot);
}

function mapLegend(color) {
  const legend = element('div', '', 'map-legend');
  legend.setAttribute('role', 'group');
  legend.setAttribute('aria-label', `${color} per KiB`);
  for (const [index, band] of heatBands.entries()) {
    legend.append(element('span', band.label, `heat-${index}`));
  }
  return legend;
}

function folderBreadcrumb(folder, folderHref) {
  const crumbs = element('nav', '', 'folder-path');
  crumbs.setAttribute('aria-label', 'Folder path');
  const path = [];
  const parts = folder.path ? ['Repository', ...folder.path.split('/')] : ['Repository'];
  for (const [index, part] of parts.entries()) {
    if (index) path.push(part);
    if (index === parts.length - 1) crumbs.append(element('strong', part));
    else crumbs.append(link(part, folderHref(path.join('/'))), element('span', '/'));
  }
  return crumbs;
}

function folderContents(children, navigation) {
  const details = element('details', '', 'folder-contents');
  const items = element('ul');
  for (const child of [...children].sort((a, b) => Number(!a.children) - Number(!b.children) || a.name.localeCompare(b.name))) {
    const item = element('li');
    item.append(link(child.children ? child.name + '/' : child.name, child.children ? navigation.folder(child.path) : navigation.file(child.path)),
      element('span', formatSize(child.bytes)));
    items.append(item);
  }
  details.append(element('summary', `Folder contents (${children.length})`), items);
  return details;
}

export function renderTreemap({files}, selected) {
  const color = selected.color === 'epics' ? 'epics' : 'stories';
  const folders = buildTree(files, color);
  const folder = folders.get(selected.folder || '');
  const folderHref = (path, metric = color, onlyFiles = false) =>
    reportHref('treemap', onlyFiles ? {folder: path, color: metric, view: 'files'} : {folder: path, color: metric});
  const section = element('section');
  if (!folder) {
    section.append(reportHeading('Repository treemap', 'This folder is missing from the current map.'),
      link('Open repository treemap', folderHref('')));
    return section;
  }
  const hasSubfolders = folder.children.some(item => item.children && item.bytes > 0);
  const onlyFiles = selected.view === 'files' && hasSubfolders;
  const navigation = {folder: folderHref, file: path => fileHref(path, folderHref(folder.path, color, onlyFiles))};
  const items = onlyFiles ? folder.children.filter(item => !item.children) : folder.children;
  const bytes = items.reduce((sum, item) => sum + item.bytes, 0);
  section.append(reportHeading('Repository treemap', 'Area shows file size. Color shows links per KiB.'));
  const controls = element('div', '', 'report-controls');
  controls.append(element('span', 'Color by'), choices('Color by', colorMetrics, color,
    metric => folderHref(folder.path, metric, onlyFiles)), mapLegend(color));
  const heading = element('div', '', 'map-heading');
  const location = folderBreadcrumb(folder, folderHref);
  if (onlyFiles) location.append(element('span', '/'), element('strong', 'Files'), link('Back to folders', folderHref(folder.path)));
  heading.append(location, element('span', formatSize(bytes), 'numeric'));
  section.append(controls, heading);
  if (items.length) section.append(folderContents(items, navigation));
  if (bytes) {
    const plot = element('div', '', 'treemap');
    plot.setAttribute('role', 'group');
    plot.setAttribute('aria-label', `File sizes in ${folder.path || 'the repository'}`);
    drawMap(plot, items, color, navigation);
    section.append(plot);
  } else {
    section.append(element('p', 'No mapped files with a positive size in this folder.', 'empty'));
  }
  return section;
}
