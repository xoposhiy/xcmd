// @implements BrowseStories, TraceStoryFiles
export const byId = id => document.getElementById(id);

export function element(tag, value = '', className = '') {
  const node = document.createElement(tag);
  node.textContent = value;
  node.className = className;
  return node;
}

export function link(label, href, className = '') {
  const node = element('a', label, className);
  node.href = href;
  return node;
}

export function sourceLink(label, path, className = '') {
  const node = link(label, '/source?path=' + encodeURIComponent(path), className);
  node.target = '_blank';
  node.rel = 'noopener';
  node.title = 'Open source text in a new tab';
  return node;
}

export function reportHref(report, params = {}) {
  return '#' + new URLSearchParams({report, ...params});
}

export function fileHref(path, from) {
  return '#' + new URLSearchParams(from ? {file: path, from} : {file: path});
}

export function formatSize(bytes) {
  if (bytes < 1024) return `${bytes} B`;
  const unit = bytes < 1024 ** 2 ? 1 : 2;
  return `${(bytes / 1024 ** unit).toLocaleString('en', {maximumFractionDigits: 1})} ${['B', 'KiB', 'MiB'][unit]}`;
}

export function reportHeading(title, description) {
  const header = element('header', '', 'report-heading');
  header.append(element('h2', title), element('p', description));
  return header;
}

export function choices(label, options, current, href) {
  const group = element('nav', '', 'choices');
  group.setAttribute('aria-label', label);
  for (const [key, title] of Object.entries(options)) {
    const choice = link(title, href(key));
    if (key === current) choice.setAttribute('aria-current', 'true');
    group.append(choice);
  }
  return group;
}
