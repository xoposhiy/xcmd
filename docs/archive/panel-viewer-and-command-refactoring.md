# Panel state, file viewing and command shortcuts

Proposed and implemented on 19 November 2026. The developer approved both stories
in Codex session ID 01a1f50a-93bf-7753-9afd-df7cf27eeaaf.

The source map review counted direct links to user and user-developer stories in 219 production C# files. Fourteen files had six or more links. The main menu had 29 and the main keyboard handler had 24. Many links are expected where the UI exposes commands for different features. This work addressed specific shared behavior and duplicate command bindings.

## PanelReturnState

See [PanelReturnState](../stories/reusable-components.md#panelreturnstate) in the LivingDoc.

[SearchResultPanel](../stories/file-search.md#searchresultpanel) saves the panel's place before showing a result snapshot. [BrowseHistory](../stories/repository-work.md#browsehistory) saves the panel's place before opening a log or snapshot. Both use [the return point store](../../src/Xcmd.Desktop/PanelReturnLocations.cs) and [the same capture and restore methods](../../src/Xcmd.Desktop/PanelView.ReturnState.cs). Restore reloads the location, restores the cursor and marks, fits the columns to the current panel width, restores scrolling, and returns focus to the panel.

The [search caller](../../src/Xcmd.Desktop/PanelView.SearchResults.cs) and [history caller](../../src/Xcmd.Desktop/PanelView.History.cs) decide when to enter or leave each view. Directory navigation keeps the saved point while the panel browses history. [WorkspaceStorage](../stories/reliable-delivery.md#workspacestorage) covers saving the workspace across application sessions.

## ReusableFileViewer

See [ReusableFileViewer](../stories/reusable-components.md#reusablefileviewer) in the LivingDoc.

Both [OpenViewers](../stories/file-inspection.md#openviewers) and [QuickViewFile](../stories/file-inspection.md#quickviewfile) use the [FileViewer control](../../src/Xcmd.Desktop/FileViewer.cs). F3 hosts the reader in a [separate window](../../src/Xcmd.Desktop/FileViewerWindow.cs). Ctrl+Q hosts it in the [opposite panel](../../src/Xcmd.Desktop/QuickViewPane.cs) and follows the selected file. The [viewing controller](../../src/Xcmd.Desktop/FileViewingController.cs) connects both readers to shared search text and theme settings.

The story covers hosting and connecting the reader to either surface. Mode selection, text search, and Git diffs each have direct mappings to the stories for those features.

## Viewer input, loading, and controls

[Viewer keyboard handling](../../src/Xcmd.Desktop/FileViewer.Keyboard.cs) routes keys and clears held keys. [Viewer loading](../../src/Xcmd.Desktop/FileViewer.Loading.cs) reads and renders files and selects the view mode. [Viewer controls](../../src/Xcmd.Desktop/FileViewer.Controls.cs) updates which controls and commands are available. These files keep direct links to the stories they implement.

## File command definitions

[Transfer definitions](../../src/Xcmd.Desktop/CommanderWindow.TransferCommands.cs) hold the action, gesture, menu label, and short hint for F5, Shift+F5, and F6. The [File menu](../../src/Xcmd.Desktop/CommanderWindow.Menu.cs), panel keyboard handler, and [shortcut hints](../../src/Xcmd.Desktop/CommanderFrame.cs) use these definitions.

The main keyboard dispatcher gives inline rename and focused editors priority. Git history keeps transfer commands disabled in the File menu, blocks their shortcuts, and shows its own hints.
