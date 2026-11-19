// @tests BrowseStories, TraceStoryFiles
async page => {
  const assert = (condition, message) => { if (!condition) throw new Error(message); };
  const waitForHeading = title => page.waitForFunction(expected => document.querySelector('main h2')?.textContent === expected, title);
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
  const base = new URL(page.url()).origin;
  await page.setViewportSize({width: 1280, height: 900});
  await page.goto(base);
  await page.waitForFunction(() => !document.getElementById('reload').disabled);
  const response = await page.request.get(base + '/api/catalogue');
  const data = await response.json();
  const epic = data.epics[0];
  const story = epic.stories[0];
  assert(await page.locator('.epic').count() === data.counts.epics, 'Every epic must appear in order.');
  assert(await page.locator('main h2').textContent() === epic.title, 'Start with the first epic.');
  await page.getByRole('navigation', {name: 'Epics and user stories'}).getByRole('link', {name: story.id, exact: true}).click();
  await waitForHeading(story.id);
  assert(await page.getByRole('table', {name: 'Implementation files', exact: true}).locator('tbody tr').count() === story.implements.length,
    'Implementation rows must match source annotations.');
  assert(await page.getByRole('table', {name: 'Test files', exact: true}).locator('tbody tr').count() === story.tests.length,
    'Test rows must match source annotations.');
  await page.screenshot({path: 'artifacts/story-explorer/story-desktop.png'});
  const popupPromise = page.waitForEvent('popup');
  await page.getByRole('table', {name: 'Implementation files', exact: true}).locator('a').first().click();
  const popup = await popupPromise;
  await popup.waitForLoadState();
  assert((await popup.locator('body').innerText()).includes('@implements'), 'A file link must open actual source text.');
  await popup.close();
  await page.getByRole('navigation', {name: 'Reading order'}).getByRole('link').last().click();
  await waitForHeading(epic.stories[1].id);
  await page.goBack();
  await waitForHeading(story.id);
  const secondEpic = page.locator('.epic').nth(1);
  await secondEpic.locator('summary').click();
  assert(await secondEpic.evaluate(node => node.open), 'An epic must expand to show its stories.');
  await secondEpic.getByRole('link', {name: 'Epic overview'}).click();
  await waitForHeading(data.epics[1].title);
  await page.goBack();
  await waitForHeading(story.id);
  await page.getByRole('searchbox').fill(story.implements[0].path);
  assert(await page.locator('.story-link').count() > 0, 'Search must find stories by file path.');
  await page.getByRole('searchbox').fill('no-such-story-or-file');
  assert(await page.locator('.story-link').count() === 0, 'Unmatched search must show an empty state.');
  await page.getByRole('searchbox').fill('');
  await page.getByRole('button', {name: 'Reload map'}).click();
  await page.waitForFunction(() => !document.getElementById('reload').disabled);
  assert(await page.locator('main h2').textContent() === story.id, 'Reload must preserve selection.');
  await page.route('**/api/catalogue', route => route.fulfill({status: 422, contentType: 'application/json', body: '{"error":"Invalid test catalogue"}'}));
  await page.getByRole('button', {name: 'Reload map'}).click();
  await page.getByRole('alert').waitFor({state: 'visible'});
  assert((await page.getByRole('alert').textContent()).includes('previous map'), 'Failed reload must identify the stale map.');
  await page.unroute('**/api/catalogue');
  await page.getByRole('button', {name: 'Reload map'}).click();
  await page.waitForFunction(() => !document.getElementById('reload').disabled);
  assert(await page.getByRole('alert').count() === 0, 'A successful reload must clear the error.');
  const emptyTestMap = structuredClone(data);
  const storyWithoutTests = emptyTestMap.epics[0].stories[0];
  storyWithoutTests.tests = [];
  await page.route('**/api/catalogue', route => route.fulfill({json: emptyTestMap}));
  await page.reload();
  await waitForHeading(storyWithoutTests.id);
  assert((await page.locator('main').textContent()).includes('No test files'), 'Stories without mapped tests need an empty state.');
  await page.unroute('**/api/catalogue');
  await page.reload();
  await page.waitForFunction(() => !document.getElementById('reload').disabled);
  await page.getByRole('link', {name: 'Skip to content'}).focus();
  await page.keyboard.press('Enter');
  await page.waitForFunction(() => document.activeElement.id === 'content');
  assert(new URL(page.url()).hash === '#story=' + story.id, 'Skip link must not change the URL.');
  assert(await page.locator('main h2').textContent() === story.id, 'Skip link must preserve the story route.');
  await page.setViewportSize({width: 390, height: 844});
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.getByRole('navigation', {name: 'Epics and user stories'}).getByRole('link', {name: story.id, exact: true}).click();
  await page.waitForFunction(() => document.activeElement.id === 'content' && Math.abs(document.getElementById('content').getBoundingClientRect().top) < 4);
  assert(await page.locator('main h2').textContent() === story.id, 'Choosing the current story again must return to its content.');
  assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Narrow layout must not overflow horizontally.');
  await page.screenshot({path: 'artifacts/story-explorer/story-mobile.png'});
  await page.setViewportSize({width: 853, height: 600});
  assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'A smaller effective viewport must remain usable.');
  await page.goto(base + '/#story=MissingStory');
  await page.waitForFunction(() => !document.getElementById('reload').disabled);
  assert(await page.locator('main h2').textContent() === 'Story not found', 'A stale deep link must be explained.');
  await page.setViewportSize({width: 1280, height: 900});
  const views = page.getByRole('navigation', {name: 'Explorer views'});
  const countRows = name => page.getByRole('table', {name, exact: true}).locator('tbody tr').evaluateAll(rows =>
    rows.map(row => [row.querySelector('a').textContent, ...[...row.querySelectorAll('td')].map(cell => Number(cell.textContent))]));
  const expectRows = async (name, rows, sort) => {
    await page.getByRole('table', {name, exact: true}).locator(`th[aria-sort="descending"]`).filter({hasText: sort.label}).waitFor();
    const expected = [...rows].sort((a, b) => b[sort.column] - a[sort.column] || a[0].localeCompare(b[0]));
    assert(JSON.stringify(await countRows(name)) === JSON.stringify(expected), `${name} must rank every row by ${sort.label}.`);
  };
  const storyCounts = data.epics.flatMap(item => item.stories.map(entry => [entry.id, entry.implements.length, entry.tests.length]));
  await views.getByRole('link', {name: 'Story rankings'}).click();
  await expectRows('Story rankings', storyCounts, {label: 'Implementation files', column: 1});
  await page.getByRole('link', {name: 'Test files', exact: true}).click();
  await expectRows('Story rankings', storyCounts, {label: 'Test files', column: 2});
  await page.screenshot({path: 'artifacts/story-explorer/rankings.png'});
  await page.getByRole('searchbox', {name: 'Filter stories'}).fill(story.id);
  assert((await countRows('Story rankings')).some(([name]) => name === story.id), 'Filter must find a known ranked story.');
  await page.getByRole('table', {name: 'Story rankings'}).getByRole('link', {name: story.id, exact: true}).click();
  await waitForHeading(story.id);

  const fileCounts = data.files.map(path => {
    const links = data.epics.map(item => item.stories.filter(entry =>
      [...entry.implements, ...entry.tests].some(file => file.path === path)));
    return [path, links.flat().length, links.filter(group => group.length).length];
  });
  await views.getByRole('link', {name: 'File rankings'}).click();
  await expectRows('File rankings', fileCounts, {label: 'Stories', column: 1});
  await page.getByRole('link', {name: 'Epics', exact: true}).click();
  await expectRows('File rankings', fileCounts, {label: 'Epics', column: 2});
  const sharedPath = (await countRows('File rankings'))[0][0];
  await page.getByRole('searchbox', {name: 'Filter paths'}).fill(sharedPath);
  assert((await countRows('File rankings')).length === 1, 'Filter must find a known ranked file.');
  await page.getByRole('link', {name: 'Stories', exact: true}).click();
  await page.getByRole('table', {name: 'File rankings'}).locator('th[aria-sort="descending"]').filter({hasText: 'Stories'}).waitFor();
  assert(await page.getByRole('searchbox', {name: 'Filter paths'}).inputValue() === sharedPath,
    'Changing the ranking metric must preserve the filter.');
  await page.getByRole('link', {name: sharedPath, exact: true}).click();
  await waitForHeading(sharedPath);
  assert((await countRows('Stories linked to this file')).length === fileCounts.find(([path]) => path === sharedPath)[1],
    'File details must show each linked story once.');
  await page.goBack();
  await waitForHeading('File rankings');
  assert(await page.getByRole('searchbox', {name: 'Filter paths'}).inputValue() === sharedPath &&
    (await countRows('File rankings')).length === 1, 'Returning to the ranking must restore the filtered results.');

  await views.getByRole('link', {name: 'Treemap'}).click();
  await waitForHeading('Repository treemap');
  const mappedTiles = page.locator('.treemap .map-file');
  const treemap = page.locator('.treemap');
  assert(await page.locator('.folder-contents summary').evaluate(summary =>
    Boolean(summary.compareDocumentPosition(document.querySelector('.treemap')) & Node.DOCUMENT_POSITION_FOLLOWING)),
    'Keyboard users must reach the folder list before the file tiles.');
  const folderLabel = path => treemap.getByRole('link', {name: `Open folder ${path}`, exact: true});
  const outline = label => label.evaluate(node => getComputedStyle(node.parentElement, '::after').boxShadow);
  const testsFolder = folderLabel('tests');
  const scenariosPath = 'tests/Xcmd.Probe/Scenarios';
  const scenarios = folderLabel(scenariosPath);
  const probe = folderLabel('tests/Xcmd.Probe');
  await testsFolder.waitFor();
  await scenarios.waitFor();
  await folderLabel('tests/Xcmd.Probe/Infrastructure').waitFor();
  const overview = await mappedTiles.evaluateAll(tiles => tiles.map(tile => {
    const path = new URLSearchParams(tile.hash.slice(1)).get('file');
    const parents = [];
    for (let folder = tile.closest('.map-folder'); folder; folder = folder.parentElement.closest('.map-folder')) {
      parents.push(folder.querySelector(':scope > .folder-label')?.getAttribute('aria-label'));
    }
    return {path, parents, heat: Number(tile.className.match(/heat-(\d)/)[1]), title: tile.title};
  }));
  assert(overview.length === data.files.filter(path => data.fileSizes[path] > 0).length &&
    new Set(overview.map(tile => tile.path)).size === overview.length, 'The repository overview must show each file once.');
  assert(overview.filter(tile => tile.path.startsWith(scenariosPath + '/')).every(tile =>
    tile.parents.join(',') === [scenariosPath, 'tests/Xcmd.Probe', 'tests'].map(path => `Open folder ${path}`).join(',')),
    'Scenario files must appear inside Scenarios, Xcmd.Probe and tests.');
  const countsByPath = new Map(fileCounts.map(([path, stories, epics]) => [path, {stories, epics}]));
  const expectedHeat = (path, metric) => [0, .25, .5, 1, Infinity].findIndex(limit =>
    1024 * countsByPath.get(path)[metric] / data.fileSizes[path] <= limit);
  assert(overview.every(tile => tile.heat === expectedHeat(tile.path, 'stories') && tile.title.startsWith(tile.path + ' · ')),
    'File colors must reflect story links per KiB, and tooltips must show full paths.');
  await page.getByRole('group', {name: 'stories per KiB', exact: true}).waitFor();
  const checkFileNames = async () => mappedTiles.evaluateAll(tiles => {
    let visible = 0;
    let hidden = 0;
    for (const tile of tiles) {
      const label = tile.querySelector('.tile-name');
      if (label.hidden && tile.title) hidden++;
      else {
        const name = label.getBoundingClientRect();
        const bounds = tile.getBoundingClientRect();
        if (name.width && name.width <= bounds.width && name.height <= bounds.height) visible++;
      }
    }
    return {visible, hidden, total: tiles.length};
  });
  const labels = await checkFileNames();
  assert(labels.visible > 0 && labels.hidden > 0 && labels.visible + labels.hidden === labels.total,
    'Show only file names that fit and give small tiles a tooltip.');
  assert(await mappedTiles.locator('.tile-name:visible').first().evaluate(name => getComputedStyle(name).fontWeight === '400'),
    'File names must use regular weight.');
  const treemapArea = await treemap.evaluate(node => node.clientWidth * node.clientHeight);
  const totalBytes = Object.values(data.fileSizes).reduce((total, bytes) => total + bytes, 0);
  const testsBytes = Object.entries(data.fileSizes).reduce((total, [path, bytes]) => total + (path.startsWith('tests/') ? bytes : 0), 0);
  const testsArea = await testsFolder.evaluate(node => node.parentElement.clientWidth * node.parentElement.clientHeight);
  assert(Math.abs(testsArea / treemapArea - testsBytes / totalBytes) < .003, 'The tests block must occupy its share of repository bytes.');
  await page.screenshot({path: 'artifacts/story-explorer/treemap.png'});

  const scenariosOutline = await outline(scenarios);
  const probeOutline = await outline(probe);
  await scenarios.hover();
  assert(await outline(scenarios) !== scenariosOutline && await outline(probe) === probeOutline,
    'Hovering the Scenarios block must outline only Scenarios.');
  await page.screenshot({path: 'artifacts/story-explorer/treemap-folder-highlight.png'});
  await scenarios.click();
  await page.getByRole('group', {name: `File sizes in ${scenariosPath}`, exact: true}).waitFor();
  const contents = data.files.filter(path => path.startsWith(scenariosPath + '/') && data.fileSizes[path] > 0);
  assert(await mappedTiles.count() === contents.length, 'Opening Scenarios must show its individual files once.');
  const positions = await mappedTiles.evaluateAll(tiles => tiles.map(tile => {
    const {width, height, left, top, right, bottom} = tile.getBoundingClientRect();
    return {path: new URLSearchParams(tile.hash.slice(1)).get('file'), width, height, left, top, right, bottom};
  }));
  const scenariosBytes = contents.reduce((total, path) => total + data.fileSizes[path], 0);
  assert(positions.every(tile => Math.abs(tile.width * tile.height / treemapArea - data.fileSizes[tile.path] / scenariosBytes) < .001),
    'Each file tile must occupy its share of the folder bytes.');
  assert(positions.filter(tile => tile.width > tile.height).length > positions.length / 2,
    'Prefer horizontal rectangles for file names.');
  const orderedPaths = [...contents].sort((a, b) =>
    expectedHeat(b, 'stories') - expectedHeat(a, 'stories') || data.fileSizes[b] - data.fileSizes[a] ||
    a.split('/').pop().localeCompare(b.split('/').pop()));
  assert(JSON.stringify(positions.map(tile => tile.path)) === JSON.stringify(orderedPaths),
    'Order file tiles by color band, then by size within each band.');
  const plotBounds = await treemap.boundingBox();
  const first = positions[0];
  const last = positions.at(-1);
  assert(Math.abs(first.left - plotBounds.x) < 1 && Math.abs(first.top - plotBounds.y) < 1 &&
    Math.abs(last.right - plotBounds.x - plotBounds.width) < 1 && Math.abs(last.bottom - plotBounds.y - plotBounds.height) < 1,
    'Place the first file at the top left and the last file at the bottom right.');
  await page.getByRole('link', {name: 'Epics', exact: true}).click();
  const epicTile = treemap.getByRole('link', {name: /epics\/KiB$/}).first();
  await epicTile.waitFor();
  const tilePath = await epicTile.evaluate(node => new URLSearchParams(node.hash.slice(1)).get('file'));
  assert((await epicTile.getAttribute('class')).includes(`heat-${expectedHeat(tilePath, 'epics')}`),
    'Changing the color metric must use epic links per KiB.');
  assert(new URLSearchParams(new URL(page.url()).hash.slice(1)).get('folder') === scenariosPath, 'Changing the color metric must preserve the folder.');
  await epicTile.click();
  await page.getByRole('link', {name: 'Open source ↗'}).waitFor();
  await page.getByRole('link', {name: '← Treemap', exact: true}).click();
  await page.getByRole('group', {name: `File sizes in ${scenariosPath}`, exact: true}).waitFor();
  await page.getByRole('group', {name: 'epics per KiB', exact: true}).waitFor();
  await page.goto(base + '/#report=treemap&view=files');
  await page.getByRole('link', {name: 'Back to folders'}).waitFor();
  await mappedTiles.first().waitFor();
  assert(await mappedTiles.count() === data.files.filter(path => !path.includes('/') && data.fileSizes[path] > 0).length,
    'A saved files-only link must show only the files directly in its folder.');

  await views.getByRole('link', {name: 'Charts'}).click();
  await waitForHeading('Mapping charts');
  const point = page.getByRole('button', {name: /^\d+ implementation files, 0 test files,/}).first();
  await point.focus();
  await page.keyboard.press('Enter');
  assert(await point.getAttribute('aria-pressed') === 'true', 'Keyboard input must select a scatter point.');
  assert((await page.locator('.point-detail').textContent()).includes('0 test files'), 'Selected point must show matching stories.');
  assert(await page.locator('.epic-bars li').count() === data.epics.length, 'The bar chart must show each epic.');
  await page.screenshot({path: 'artifacts/story-explorer/charts.png'});

  await views.getByRole('link', {name: 'Matrix', exact: true}).click();
  await waitForHeading('Implementation matrix');
  const matrix = page.getByRole('img', {name: `Implementation matrix, ${data.matrixFiles.length} files by ${data.counts.stories} stories`});
  const fileOrder = page.getByRole('combobox', {name: 'Files'});
  const storyOrder = page.getByRole('combobox', {name: 'Stories'});
  const withinDirectory = page.getByRole('combobox', {name: 'Within directories'});
  const tip = page.locator('.matrix-tip');
  const epicHeaders = page.getByRole('group', {name: 'Story epics'}).getByRole('button');
  const directoryHeaders = page.getByRole('group', {name: 'Source directories'}).getByRole('button');
  const storyColumns = data.epics.flatMap(epic => epic.stories.map(story => ({epic: epic.id, ...story})));
  await matrix.waitFor();
  assert(await epicHeaders.count() === data.epics.length && await directoryHeaders.count() === 0,
    'Epic order must show one header per epic. Directory headers require directory order.');
  await page.screenshot({path: 'artifacts/story-explorer/matrix.png', fullPage: true});
  await epicHeaders.first().hover();
  assert(await tip.isVisible() && await tip.textContent() === data.epics[0].title, 'Hovering on an epic must show its full title.');
  await matrix.focus();
  await page.keyboard.press('ArrowRight');
  assert(await tip.locator('strong').textContent() === storyColumns[1].id, 'Arrow keys must inspect the next story.');
  await page.keyboard.press('Escape');
  await tip.waitFor({state: 'hidden'});
  await fileOrder.selectOption('folders');

  const directoryOf = path => path.slice(0, path.lastIndexOf('/'));
  const directoryNames = new Set();
  for (const path of data.matrixFiles) {
    const parts = directoryOf(path).split('/');
    for (let depth = Math.min(2, parts.length); depth <= parts.length; depth++) {
      directoryNames.add(parts.slice(0, depth).join('/'));
    }
  }
  const pathKey = path => path.replaceAll('/', '\0');
  const directories = [...directoryNames].sort((a, b) => pathKey(a) < pathKey(b) ? -1 : 1);
  const filesByDirectory = directories.map(directory => data.matrixFiles.filter(path => directoryOf(path) === directory));
  const matrixPaths = filesByDirectory.flat();
  const maxBytes = Math.max(1, ...Object.values(data.matrixFileSizes));
  const fileHeight = path => Math.max(3, Math.round(16 * Math.sqrt(data.matrixFileSizes[path] / maxBytes)));
  assert(JSON.stringify(await directoryHeaders.evaluateAll(headers => headers.map(header => header.getAttribute('aria-label')))) ===
    JSON.stringify(directories), 'Directory order must show the full directory tree in order.');
  const firstDirectory = directoryHeaders.first();
  await firstDirectory.hover();
  assert(await tip.isVisible() && await tip.textContent() === directories[0], 'Hovering on a source directory must show its path.');
  assert((await firstDirectory.boundingBox()).height === Math.max(28, filesByDirectory[0].reduce((sum, path) => sum + fileHeight(path), 0)),
    'A directory header must cover all its file rows.');
  await page.mouse.move(2, 2);
  await firstDirectory.focus();
  assert(await tip.isVisible() && await tip.textContent() === directories[0], 'Keyboard focus must show the directory path.');
  await page.keyboard.press('Escape');
  await tip.waitFor({state: 'hidden'});
  const nestedPath = directories.find(path => path.split('/').length > 2);
  const nestedHeader = page.getByRole('button', {name: nestedPath, exact: true});
  const parentHeader = page.getByRole('button', {name: directoryOf(nestedPath), exact: true});
  assert((await nestedHeader.boundingBox()).x > (await parentHeader.boundingBox()).x, 'Subdirectory headers must be indented.');
  await nestedHeader.hover();
  assert(await tip.isVisible() && await tip.textContent() === nestedPath, 'Subdirectory details must show the full path.');
  await page.mouse.move(2, 2);
  const transitionCount = async () => parseInt(await page.locator('.matrix-transitions').textContent(), 10);
  assert(await withinDirectory.inputValue() === 'transitions', 'Directory order must default to fewer transitions.');
  const reduced = await transitionCount();
  await withinDirectory.selectOption('shared');
  const sharedStoryTransitions = await transitionCount();
  await withinDirectory.selectOption('paths');
  assert(reduced <= Math.min(sharedStoryTransitions, await transitionCount()),
    'The reduced order must have at most as many transitions as the other orders.');
  await withinDirectory.selectOption('transitions');
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({path: 'artifacts/story-explorer/matrix-directories.png', fullPage: true});
  await withinDirectory.selectOption('paths');

  const offsets = (groups, size) => {
    let gap = 0;
    return groups.map((group, index) => {
      if (index && group !== groups[index - 1]) gap += 3;
      return index * size + gap;
    });
  };
  const x = offsets(storyColumns.map(story => story.epic), 10);
  const y = [];
  let end = 0;
  for (const [i, paths] of filesByDirectory.entries()) {
    if (i) end += 3;
    const top = end;
    for (const path of paths) {
      y.push(end);
      end += fileHeight(path);
    }
    end = Math.max(end, top + 28);
  }
  assert(await matrix.evaluate(canvas => canvas.clientHeight) === end, 'The grid height must match its file rows and directory groups.');
  const linkedPaths = storyColumns.map(story => new Set(story.implements.map(file => file.path)));
  const expected = matrixPaths.map(path => linkedPaths.map(paths => paths.has(path)));
  const wrongCells = await matrix.evaluate((canvas, {x, y, expected}) => {
    const ratio = canvas.width / canvas.clientWidth;
    const pixels = canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height).data;
    let wrong = 0;
    for (const [row, top] of y.entries()) {
      for (const [column, left] of x.entries()) {
        const pixel = (Math.floor(top * ratio) * canvas.width + Math.floor(left * ratio)) * 4;
        const color = pixels.slice(pixel, pixel + 3).join(',');
        if (color !== (expected[row][column] ? '35,131,79' : '255,255,255')) wrong++;
      }
    }
    return wrong;
  }, {x, y, expected});
  assert(!wrongCells, 'Every matrix cell must match its source implementation link in folder and epic order.');
  const firstLinkedRow = expected.findIndex(row => row[0]);
  const firstUnlinkedRow = expected.findIndex(row => !row[0]);
  const largestRow = matrixPaths.findIndex(path => data.matrixFileSizes[path] === maxBytes);
  for (const row of new Set([firstLinkedRow, firstUnlinkedRow, largestRow])) {
    const height = fileHeight(matrixPaths[row]);
    await matrix.hover({position: {x: 1, y: y[row] + height - 2}});
    const text = await tip.textContent();
    assert(await tip.isVisible() && text.includes(matrixPaths[row]) &&
      text.includes(expected[row][0] ? 'Implements' : 'Unlinked') && /\d (B|KiB|MiB)$/.test(text),
    'Pointer details must show the file, its link state and its size.');
    assert(await page.locator('.matrix-mark').evaluate(mark => mark.offsetHeight) === height, 'The highlight must match the row height.');
  }
  await page.screenshot({path: 'artifacts/story-explorer/matrix-tooltip.png'});
  const sharedHeight = expected.reduce((height, row, index) =>
    height + (row.filter(Boolean).length > 1 ? fileHeight(matrixPaths[index]) : 0), 0);
  await fileOrder.selectOption('links');
  await matrix.hover({position: {x: 1, y: sharedHeight + 4}});
  assert((await tip.textContent()).includes('1 story in this file'), 'Single-story files must follow the shared files.');
  await fileOrder.selectOption('folders');
  await storyOrder.selectOption('links');
  await fileOrder.selectOption('dependencies');
  assert(await epicHeaders.count() === 0 && await directoryHeaders.count() === 0,
    'Headers must follow the order selected for their own axis.');
  await page.getByText('Dependencies come first', {exact: false}).waitFor();
  await page.reload();
  assert(await fileOrder.inputValue() === 'dependencies' && await storyOrder.inputValue() === 'links',
    'A reload must preserve both order settings.');
  await matrix.focus();
  assert((await tip.textContent()).includes(data.dependencyGroups[0][0]), 'Dependency order must start with the first file in the graph order.');

  await page.setViewportSize({width: 390, height: 844});
  await fileOrder.selectOption('folders');
  await storyOrder.selectOption('epics');
  assert(await withinDirectory.inputValue() === 'paths', 'The within-directory order must survive reloads and view changes.');
  assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth &&
    document.querySelector('.matrix-viewport').scrollWidth > document.querySelector('.matrix-viewport').clientWidth),
  'Only the matrix itself may scroll horizontally on a narrow screen.');
  assert(await page.locator('.matrix-viewport').evaluate(view => {
    const header = view.querySelector('.matrix-directories');
    const initial = header.getBoundingClientRect().left;
    view.scrollLeft = 280;
    return view.scrollLeft > 0 && Math.abs(header.getBoundingClientRect().left - initial) <= 4;
  }), 'Directory headers must remain visible when scrolling across stories.');
  for (const [view, title] of [['Charts', 'Mapping charts'], ['Treemap', 'Repository treemap'],
    ['File rankings', 'File rankings'], ['Story rankings', 'Story rankings']]) {
    await views.getByRole('link', {name: view, exact: true}).click();
    await waitForHeading(title);
    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${view} must fit a narrow screen.`);
    if (view === 'Treemap') {
      await mappedTiles.first().waitFor();
      const labels = await checkFileNames();
      assert(labels.visible + labels.hidden === labels.total, 'Keep all small tiles accessible through file links and tooltips.');
      await page.screenshot({path: 'artifacts/story-explorer/treemap-mobile.png'});
      await folderLabel(scenariosPath).click();
      await mappedTiles.first().waitFor();
      const zoomedLabels = await checkFileNames();
      assert(zoomedLabels.visible > 0 && zoomedLabels.visible + zoomedLabels.hidden === zoomedLabels.total,
        'Show the file names that fit after zooming into a folder on a narrow screen.');
    }
  }
  await page.screenshot({path: 'artifacts/story-explorer/rankings-mobile.png'});
  const unexpected = errors.filter(message => !message.includes('422'));
  assert(!unexpected.length, 'Unexpected browser errors: ' + unexpected.join('; '));
  return {epics: data.counts.epics, stories: data.counts.stories, mappedFiles: data.counts.files,
    checks: 'catalogue, reload, source, all four rankings, file details, treemap, charts, matrix, keyboard, narrow layout'};
}
