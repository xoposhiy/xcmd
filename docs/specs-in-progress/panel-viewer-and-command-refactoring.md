# Panel state, file viewing and command shortcuts

Proposed on 19 November 2026. The two story drafts await review. The refactorings are planned.

The source map review counted direct links to user and user-developer stories in 219 production C# files. Fourteen files had six or more links. The main menu had 29 and the main keyboard handler had 24. Many links are expected where the UI exposes commands for different features. The proposals below address specific shared behavior and duplicated decisions.

## First story to review: PanelReturnState

Proposed epic: [Reuse shared components across features](../stories/reusable-boundaries.md).

> As a developer, I want to save and restore a panel’s place when a temporary view uses it so that search results and Git history can return the user to the same folder, selection and scroll position.

[SearchResultPanel](../stories/file-search.md#searchresultpanel) and [BrowseHistory](../stories/repository-work.md#browsehistory) both save a return point before taking over a panel. Both exit through the same restore method. It restores the cursor and marks, fits the columns to the current width, restores scrolling and returns focus to the panel. [Return point](../../src/Xcmd.Desktop/PanelReturnLocations.cs) · [Restore method](../../src/Xcmd.Desktop/PanelView.cs) · [Search caller](../../src/Xcmd.Desktop/PanelView.SearchResults.cs) · [History caller](../../src/Xcmd.Desktop/PanelView.History.cs)

If the story is accepted, map the shared return-point code to it. Keep the decision to enter or leave each temporary view with its feature. Saving the workspace across application sessions remains part of `WorkspaceStorage`.

## Second story to review: ReusableFileViewer

Proposed epic: [Reuse shared components across features](../stories/reusable-boundaries.md).

> As a developer, I want to host the same file reader in a window or a panel so that both views can use the same reading commands.

F3 opens a reader in a separate window. Ctrl+Q embeds the same reader in the opposite panel and follows the selected file. The quick-view story already records this reuse as an implementation decision. [Window host](../../src/Xcmd.Desktop/FileViewerWindow.cs) · [Panel host](../../src/Xcmd.Desktop/FileViewingController.cs) · [Quick-view decision](../stories/file-inspection.md#quickviewfile)

Limit this candidate to hosting the reader and connecting it to either surface. Keep direct story links for code that chooses an image mode, finds text or displays a diff. Review those boundaries before adding the card.

## First refactoring: separate viewer input from loading

The [viewer loading file](../../src/Xcmd.Desktop/FileViewer.Loading.cs) has ten user-story links. It reads and renders files, updates command availability and handles keyboard input.

1. Move `OnViewerKey` and its key handlers into a dedicated part of `FileViewer`. Move their direct story links with them.
2. Review the remaining boundary between reading a page and updating controls. Keep file loading, mode selection and command availability traceable to the features they implement.
3. Check keyboard navigation and mode changes in both F3 and quick view. The same keys should still work with the same focus and file state.

## Second refactoring: share file command definitions

F5 is declared in the menu, handled in the keyboard dispatcher and written again in the shortcut hints. The menu and keyboard already call the same copy action. The duplication is in how the action is bound and described. [Menu](../../src/Xcmd.Desktop/CommanderWindow.Menu.cs) · [Keyboard](../../src/Xcmd.Desktop/CommanderWindow.Keyboard.cs) · [Shortcut hints](../../src/Xcmd.Desktop/CommanderFrame.cs)

Start with copy and move. Define each action, gesture and label once and use the definition for the menu, keyboard and hints. Keep context rules explicit for inline rename, Git history and focused editors. Check F5, Shift+F5 and F6 from the panel, their menu equivalents, and the commands shown or blocked in each context.
