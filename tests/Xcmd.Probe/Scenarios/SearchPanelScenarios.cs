// @tests OpenOtherPanel, SortEntries, ReadablePanels, RefreshEntries, MarkEntries, RenameEntry, TransferEntries, SearchResultPanel, OpenViewers, BrowseArchives, RunCommand, RestoreWorkspace, PanelReturnState
using Avalonia.Input;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class SearchPanelScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("search-panel-command-working-directory", "UC31; commands use the directory shown in the prompt while results span folders", async c =>
        {
            SearchProbe.TextFile(c, "first/command.txt", "first");
            SearchProbe.TextFile(c, "second/command.txt", "second");
            var search = await SearchProbe.OpenAsync(c, "command.txt");
            await c.KeyAsync(Key.P, RawInputModifiers.Alt, search);
            foreach (var folder in new[] { "first", "second" })
            {
                await c.SelectAsync(Path.Combine(folder, "command.txt"));
                var label = c.Window.CommandLine.GetVisualDescendants().OfType<TextBlock>()
                    .Single(text => AutomationProperties.GetAutomationId(text) == "command-line.directory");
                Equal(char.ToLowerInvariant(c.LeftPath[0]) + c.LeftPath[1..], ToolTip.GetTip(label) as string, "Prompt displays the search origin with a lowercase drive letter");
                await c.TextAsync("tool relative.txt");
                await c.KeyAsync(Key.Enter);
                Equal(c.LeftPath, c.Launcher.Requests[^1].WorkingDirectory, "Relative command runs in the displayed folder");
                c.Window.Left.FocusCurrent();
                await c.SettleAsync();
                Check(c.Window.Left.IsSearchResults, "Running a command keeps results available");
            }
        }),
        new("search-panel-rename-preserves-identity", "UC31; renaming across sort positions keeps cursor and marks on the renamed file", async c =>
        {
            SearchProbe.TextFile(c, "nested/aaa.txt", "a");
            SearchProbe.TextFile(c, "nested/bbb.txt", "b");
            SearchProbe.TextFile(c, "nested/ccc.txt", "c");
            var search = await SearchProbe.OpenAsync(c, "*.txt");
            await c.KeyAsync(Key.P, RawInputModifiers.Alt, search);
            var panel = c.Window.Left;
            await c.SelectAsync(Path.Combine("nested", "aaa.txt"));
            await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            await c.KeyAsync(Key.Up);
            await c.KeyAsync(Key.F6, RawInputModifiers.Shift);
            panel.EditingRow!.DraftName = "zzz.txt";
            await c.KeyAsync(Key.Enter);
            Equal(Path.Combine("nested", "zzz.txt"), panel.Current?.Name, "Cursor follows renamed file across sort positions");
            Check(panel.Current!.IsMarked, "Renamed file retains its mark");
            Check(panel.SelectedNames().SequenceEqual(new[] { Path.Combine("nested", "zzz.txt") }), "Selection uses the renamed result identity");
        }),
        new("search-panel-locate-and-return", "UC31; real search results, duplicate names, opposite-panel location, Escape restoration and saved workspace", async c =>
        {
            SearchProbe.TextFile(c, "first/same.txt", "first");
            SearchProbe.TextFile(c, "second/same.txt", "second");
            await c.Window.Left.LoadAsync(c.LeftPath);
            await c.SelectAsync("first");
            var panel = c.Window.Left;
            panel.Table.Columns[0].Width = new GridLength(100);
            panel.Table.Columns[1].Width = new GridLength(125);
            var before = panel.CaptureState();
            var search = await SearchProbe.OpenAsync(c, "same.txt");
            Equal(2, SearchProbe.Results(search).Length, "Both same-named files found");
            await c.KeyAsync(Key.P, RawInputModifiers.Alt, search);
            Check(panel.IsSearchResults && c.Window.FileSearch.Window == null, "Alt+P transfers results and closes search");
            Equal(2, panel.Rows.Count, "No parent or unrelated rows in result panel");
            Equal(2, panel.Rows.Select(row => row.Name).Distinct().Count(), "Relative paths distinguish duplicate names");
            var wanted = Path.Combine(c.LeftPath, "second", "same.txt");
            panel.SelectRow(panel.Rows.Single(row => row.Entry.FullPath == wanted));
            await c.KeyAsync(Key.Right, RawInputModifiers.Control);
            Equal(Path.GetDirectoryName(wanted), c.Window.Right.Session.DirectoryPath, "Ctrl+Right opens containing directory");
            Equal(wanted, c.Window.Right.Current?.Entry.FullPath, "Ctrl+Right selects exact file");
            Check(c.Window.ActivePanel == panel && panel.IsSearchResults, "Source panel and keyboard focus stay in results");
            using (var updates = new Xcmd.Desktop.Workspace.DirectoryUpdates(c.Window)) await updates.PollAsync();
            Check(panel.IsSearchResults, "Monitoring cannot replace search results");
            await c.KeyAsync(Key.F6, RawInputModifiers.Control);
            Equal(wanted, panel.Current?.Entry.FullPath, "Sorting keeps cursor identity");
            var saved = c.Window.CaptureWorkspace();
            var sessionFile = Path.Combine(c.FixturePath, "search-panel-session.json");
            var store = new SessionStore(sessionFile);
            await store.SaveAsync(saved);
            c.Window.Width = 1480;
            await c.SettleAsync();
            await c.KeyAsync(Key.Escape);
            Check(!panel.IsSearchResults, "Escape leaves results");
            Equal(before.DirectoryPath, panel.Session.DirectoryPath, "Escape restores original folder");
            Equal(before.Cursor, panel.Current?.Name, "Escape restores original cursor");
            Check(before.Selected.SequenceEqual(panel.SelectedNames()), "Escape restores original selection");
            Check(panel.Table.Columns[0].ActualWidth > 100, "Name fits the resized window when leaving search results");
            Equal(125d, panel.Table.Columns[1].Width.Value, "Leaving search results preserves the custom Type width");
            await c.Window.RestoreWorkspaceAsync((await store.ReadAsync())!);
            Check(panel.IsSearchResults && panel.Rows.Count == 2, "Saved workspace restores results");
            Equal(wanted, panel.Current?.Entry.FullPath, "Saved workspace restores result cursor");
            await c.ScreenshotAsync("search-result-panel");
            await c.KeyAsync(Key.Escape);
            Equal(before.Cursor, panel.Current?.Name, "Restored results retain original return cursor");
        }),
        new("search-panel-return-yields-to-navigation", "A newer transition owns the rows and focus when returning from search is interrupted", async c =>
        {
            SearchProbe.TextFile(c, "return-race.txt", "Search result");
            await c.SelectAsync("alpha.txt");
            var panel = c.Window.Left;
            var folderSort = panel.Session.SortOrder;
            var search = await SearchProbe.OpenAsync(c, "return-race.txt");
            await c.KeyAsync(Key.P, RawInputModifiers.Alt, search);
            await c.KeyAsync(Key.F6, RawInputModifiers.Control);
            var resultSort = panel.Session.SortOrder;
            var resultCursor = panel.Current?.Name;
            var resultSelection = panel.SelectedNames();
            Check(resultSort != folderSort, "Search results use a different sort from the saved folder.");

            var returnRead = c.FileSystem.PauseNextRead(c.LeftPath);
            var returning = panel.ExitSearchResultsAsync();
            await returnRead.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var destination = Path.Combine(c.LeftPath, "folder1");
            var nextRead = c.FileSystem.PauseNextRead(destination);
            var navigating = panel.LoadAsync(destination, selectName: "inside.txt");
            try
            {
                await nextRead.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                c.Window.Right.FocusCurrent();
                await returning.WaitAsync(TimeSpan.FromSeconds(5));
                Check(returnRead.WasCancelled, "The newer navigation cancels the return read.");
                Check(panel.IsSearchResults, "The displayed results stay in place until the destination is ready.");
                Equal(resultSort, panel.Session.SortOrder, "The interrupted return preserves the current sort");
                Equal(resultCursor, panel.Current?.Name, "The interrupted return preserves the current cursor");
                Check(resultSelection.SequenceEqual(panel.SelectedNames()), "The interrupted return preserves the current selection.");
                Check(c.Window.Right.Table.IsKeyboardFocusWithin, "Focus stays in the panel the user chose.");
            }
            finally { nextRead.Release.TrySetResult(); }

            Check(await navigating.WaitAsync(TimeSpan.FromSeconds(5)), "The newer navigation finishes.");
            Equal(destination, panel.Session.DirectoryPath, "The newer destination stays open");
            Equal("inside.txt", panel.Current?.Name, "The newer navigation supplies the cursor");
        }),
        new("search-panel-file-operations", "UC31; viewing, renaming, copying and moving real nested search results", async c =>
        {
            SearchProbe.TextFile(c, "nested/work.txt", "search panel content");
            var search = await SearchProbe.OpenAsync(c, "work.txt");
            await c.KeyAsync(Key.P, RawInputModifiers.Alt, search);
            var panel = c.Window.Left;
            await c.KeyAsync(Key.F3);
            Check(c.Window.Viewer != null, "F3 opens the result viewer");
            c.Window.Viewer!.Close(); await c.SettleAsync(); panel.FocusCurrent();
            await c.KeyAsync(Key.F6, RawInputModifiers.Shift);
            Equal("work.txt", panel.EditingRow?.DraftName, "Rename edits only the file name");
            panel.EditingRow!.DraftName = "renamed.txt";
            await c.KeyAsync(Key.Enter);
            Check(panel.IsSearchResults && panel.Rows.Single().Entry.Name == "renamed.txt", "Rename retains the result panel");
            Equal("search panel content", File.ReadAllText(Path.Combine(c.LeftPath, "nested", "renamed.txt")), "Rename applies to the actual nested file");
            await FileOperationProbe.CopyAsync(c);
            Equal("search panel content", File.ReadAllText(Path.Combine(c.RightPath, "renamed.txt")), "F5 copies the actual nested result");
            File.Delete(Path.Combine(c.RightPath, "renamed.txt"));
            await FileOperationProbe.MoveAsync(c);
            Check(!File.Exists(Path.Combine(c.LeftPath, "nested", "renamed.txt")), "F6 moves the actual result");
            Check(panel.IsSearchResults && panel.Rows.Count == 0, "Moved entries disappear without leaving results");
            await c.KeyAsync(Key.Escape);
            Check(!panel.IsSearchResults, "Escape works on an empty result panel");
        }),
        new("search-panel-refresh-and-archive-locate", "UC31; result removal after deletion and location of archive members", async c =>
        {
            SearchProbe.TextFile(c, "folder/member.txt", "ordinary");
            ArchiveProbe.Zip(Path.Combine(c.LeftPath, "files.zip"), ("nested/member.txt", "archive"));
            var search = await SearchProbe.OpenAsync(c, "member.txt");
            await c.KeyAsync(Key.P, RawInputModifiers.Alt, search);
            var panel = c.Window.Left;
            Equal(2, panel.Rows.Count, "Ordinary file and archive member transferred");
            var member = panel.Rows.Single(row => Xcmd.Core.ArchivePath.IsEntry(row.Entry.FullPath));
            panel.SelectRow(member);
            await c.KeyAsync(Key.Right, RawInputModifiers.Control);
            Equal(member.Entry.FullPath, c.Window.Right.Current?.Entry.FullPath, "Archive location selected in opposite panel");
            var ordinary = panel.Rows.Single(row => !Xcmd.Core.ArchivePath.IsEntry(row.Entry.FullPath));
            File.Delete(ordinary.Entry.FullPath);
            await c.KeyAsync(Key.R, RawInputModifiers.Control);
            Check(panel.IsSearchResults && panel.Rows.Count == 1, "Reload removes missing entries and keeps results mode");
            await c.KeyAsync(Key.Escape);
            Check(!panel.IsSearchResults, "Escape restores directory after archive result navigation");
        })
    ];
}
