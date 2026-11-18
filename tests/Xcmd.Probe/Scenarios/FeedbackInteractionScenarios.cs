// @tests ReportControl, KeepFeedback, FeedbackIdentity, FeedbackQueue
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Desktop;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.FeedbackProbe;

namespace Xcmd.Probe.Scenarios;

public static class FeedbackInteractionScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("feedback-first-click-immediate", "Feedback opens on the first released click without waiting for double-click detection", async c =>
        {
            for (var index = 0; index < 3; index++)
            {
                await c.KeyAsync(Key.F12);
                SelectImmediately(c.Window, c.Window.CommandLine.Editor);
                Check(c.Window.Feedback.ActiveComment != null, "The comment exists before the next dispatcher tick");
                await c.KeyAsync(Key.Escape, target: c.Window.Feedback.ActiveComment);
            }
        }),
        new("feedback-literal-table-cell-targets", "UC39; comments preserve source IDs and file names for cells, icons and Git headers", async c =>
        {
            try
            {
                await GitRowsFixture.CreateAsync(c);
                foreach (var (name, sourceId) in new[] { ("zeta.md", "PanelView.gitStaged"), ("alpha.txt", "PanelView.text"), ("dirty", "PanelView.icon") })
                {
                    var row = c.Row(name);
                    var target = row.GetVisualDescendants().OfType<Control>().First(control => control.GetValue(FeedbackTarget.IdProperty) == sourceId && (sourceId != "PanelView.text" || control is TextBlock { Text: "11" }));
                    await c.KeyAsync(Key.F12); await c.ClickAsync(target);
                    var comment = c.Window.Feedback.ActiveComment!;
                    Check(comment != null, "The actual clicked cell opens feedback");
                    await c.TextAsync("Precise cell feedback", comment); await c.KeyAsync(Key.Enter, RawInputModifiers.Control, comment);
                    var issue = c.Window.Feedback.Notes.Last();
                    Equal(sourceId, issue.Target.Id, "A source-defined cell keeps its literal ID rather than the table ID");
                    Check(issue.Target.Name.Contains(name, StringComparison.Ordinal), "The issue also identifies the clicked file or folder");
                }
                var header = c.Window.Left.GetVisualDescendants().OfType<TextBlock>().Single(text => text.GetValue(FeedbackTarget.IdProperty) == "PanelView.gitHeader");
                await c.KeyAsync(Key.F12); await c.ClickAsync(header);
                var headerComment = c.Window.Feedback.ActiveComment!;
                await c.TextAsync("Git heading feedback", headerComment); await c.KeyAsync(Key.Enter, RawInputModifiers.Control, headerComment);
                Equal("PanelView.gitHeader", c.Window.Feedback.Notes.Last().Target.Id, "A nonsortable header keeps its precise source identity");
            }
            finally { GitFixture.AllowCleanup(c.LeftPath); }
        }),
        new("feedback-control-click-menu-and-saved-context", "UC39; interact with menus while selecting feedback and save the click path", async c =>
        {
            var menu = c.Window.GetVisualDescendants().OfType<Menu>().Single();
            var file = (MenuItem)menu.Items[0]!;
            await c.KeyAsync(Key.F12);
            await UseControlAsync(c, c.Window, file, RawInputModifiers.Alt);
            Check(c.Window.IsMenuOpen && c.Window.Feedback.IsPicking, "Alt+click opens a menu without ending feedback mode");
            Check(c.Window.Feedback.ActiveComment == null, "Alt+click does not open a premature comment");
            var rename = file.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == "Rename");
            await c.ClickAsync(rename);
            var comment = c.Window.Feedback.ActiveComment!;
            Check(comment != null && c.Window.ActivePanel.EditingRow == null, "Single-click selects a menu command for comment without running it");
            Check(Text(comment!, "Actions: File > Rename").IsVisible, "The comment form shows its action context");
            await c.TextAsync("Explain this command", comment); await c.KeyAsync(Key.Enter, RawInputModifiers.Control, comment);
            var note = c.Window.Feedback.Notes.Last();
            Equal("Rename", note.Target.Name, "The note identifies the chosen menu item");
            Check(note.Target.Id.Contains("Menu", StringComparison.Ordinal), "The target identifies the source menu definition");
            var store = new SessionStore(Path.Combine(c.FixturePath, "feedback-session.json"));
            await store.SaveAsync(c.Window.CaptureWorkspace());
            Check((await store.ReadAsync())!.Notes == null, "New sessions do not contain feedback");
            Equal(note, (await c.Window.Feedback.Store!.ReadAsync()).Single(), "The independent store preserves the clicked target");
        }),
        new("feedback-menu-hover-outline", "Feedback outlines menu items while they are visible and clears the outline when they disappear", async c =>
        {
            var menu = c.Window.GetVisualDescendants().OfType<Menu>().Single();
            var file = (MenuItem)menu.Items[0]!;
            var rename = file.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == "Rename");
            await c.ClickAsync(WorkspaceProbe.Button(c.Window, "StartFeedback"));
            await UseControlAsync(c, c.Window, file);
            await CheckHighlightAsync(c, c.Window, rename, "feedback-menu-outline-100");
            menu.Close();
            await c.SettleAsync();
            Check(!HasHighlight(c.Window), "Closing the menu clears the outline without moving the pointer");

            c.Window.SetScaleForTest(1.5);
            await UseControlAsync(c, c.Window, file);
            await CheckHighlightAsync(c, c.Window, rename, "feedback-menu-outline-150");
            await c.ClickAsync(rename);
            var comment = c.Window.Feedback.ActiveComment;
            Check(comment != null, "The highlighted menu item opens a comment");
            Check(Text(comment!, "Comment on: Rename").IsVisible, "The dialog identifies the selected menu item");
            Check(!HasHighlight(c.Window), "Selecting the item clears the outline before the comment dialog opens");
        }),
        new("feedback-popup-git-actions", "Repository actions can be opened and reported item by item in feedback mode", async c =>
        {
            await GitFixture.InitializeAsync(c.RightPath);
            try
            {
                var git = c.Window.Right.Git;
                await GitFixture.RefreshAsync(git);
                await c.ClickAsync(WorkspaceProbe.Button(c.Window, "StartFeedback"));
                await UseControlAsync(c, c.Window, git.ActionsButton);
                Check(git.Actions.IsOpen, "Ctrl+click opens the right repository actions");
                await CheckHighlightAsync(c, c.Window, git.CommitCommand, "feedback-git-actions");
                await c.ClickAsync(git.CommitCommand);
                var comment = c.Window.Feedback.ActiveComment;
                Check(comment != null, "The repository action opens a comment");
                Check(Text(comment!, "Comment on: right: Commit repository with Claude Code…").IsVisible, "The comment identifies the action and its panel");
                Equal(0, c.Launcher.Requests.Count, "Selecting the action for feedback does not launch it");
                await c.KeyAsync(Key.Escape, target: comment);

                await c.ClickAsync(git.ActionsButton);
                await c.KeyAsync(Key.F12);
                var push = GitRemoteFixture.Menu(git, "Push");
                Check(!push.IsEffectivelyEnabled, "Push is disabled without a remote");
                await CheckHighlightAsync(c, c.Window, push, "feedback-disabled-git-action");
                await c.ClickAsync(push);
                Check(c.Window.Feedback.ActiveComment is { } disabled && Text(disabled, "Comment on: right: Push").IsVisible,
                    "Even a disabled action can be reported in an already open menu");
            }
            finally { GitFixture.AllowCleanup(c.RightPath); }
        }),
        new("feedback-popup-search-options", "Combo box and query history entries can be reported without selecting them", async c =>
        {
            var search = await SearchProbe.OpenAsync(c, "alpha", "abc");
            var combo = search.GetVisualDescendants().OfType<ComboBox>().Single();
            await c.KeyAsync(Key.F12, target: search);
            await UseControlAsync(c, search, combo);
            Check(combo.IsDropDownOpen, "Ctrl+click opens the search name options");
            var option = search.GetVisualDescendants().OfType<ComboBoxItem>().Single(item => Equals(item.Content, "Whole name"));
            await CheckHighlightAsync(c, search, option, "feedback-combo-option");
            await c.ClickAsync(option, target: search);
            Check(c.Window.Feedback.ActiveComment is { }, "The option opens a comment");
            Equal("Contains", combo.SelectedItem, "Reporting an option leaves the search mode alone");
            await c.TextAsync("Search name option", c.Window.Feedback.ActiveComment);
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control, c.Window.Feedback.ActiveComment);
            var issue = c.Window.Feedback.Notes.Last();
            Equal("FileSearchWindow.nameMode", issue.Target.Id, "The generated item uses its combo box's source ID");
            Check(issue.Target.Name.EndsWith("Whole name", StringComparison.Ordinal), "The report names the individual option");

            search.NameEditor.Focus();
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt, search);
            var history = search.GetVisualDescendants().OfType<ListBoxItem>().Single(item => Equals(item.Content, "alpha"));
            await c.KeyAsync(Key.F12, target: search);
            await CheckHighlightAsync(c, search, history, "feedback-query-history");
            await c.ClickAsync(history, target: search);
            Check(c.Window.Feedback.ActiveComment != null, "Feedback can start with a query popup already open");
        }),
        new("feedback-popup-reader-menus", "Nested reader menus and the text context menu share feedback selection", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.F3);
            var viewer = c.Window.Viewer!;
            await c.KeyAsync(Key.F12, target: viewer);
            await UseControlAsync(c, viewer, (MenuItem)viewer.Reader.Commands.Items[1]!);
            var themes = viewer.GetVisualDescendants().OfType<MenuItem>().Single(item => item.Name == "ViewerThemes");
            await UseControlAsync(c, viewer, themes);
            var option = themes.Items.OfType<MenuItem>().Single(item => item.Name == "viewer.theme.github");
            await CheckHighlightAsync(c, viewer, option, "feedback-nested-theme");
            await c.ClickAsync(option, target: viewer);
            Check(c.Window.Feedback.ActiveComment != null, "The nested theme opens a comment");
            await c.KeyAsync(Key.Escape, target: c.Window.Feedback.ActiveComment);

            await c.KeyAsync(Key.F12, target: viewer);
            await UseControlAsync(c, viewer, viewer.Reader.TextContent, button: MouseButton.Right);
            var contextMenu = viewer.Reader.TextContent.ContextMenu!;
            Check(contextMenu.IsOpen, "Ctrl+right-click opens the text context menu");
            var copy = contextMenu.Items.OfType<MenuItem>().Single(item => item.Name == "viewer.context-copy");
            await CheckHighlightAsync(c, viewer, copy, "feedback-viewer-context");
            await c.ClickAsync(copy, target: viewer);
            Check(c.Window.Feedback.ActiveComment != null && !contextMenu.IsOpen, "The context command opens a comment and dismisses its popup");
        }),
        new("feedback-popup-agent-menu", "The refinement list can report a flyout item without starting an agent", async c =>
        {
            await c.ClickAsync(WorkspaceProbe.Button(c.Window, "ShowNotes"));
            var notes = c.Window.Feedback.ActiveNotes!;
            var split = Control<SplitButton>(notes, "FeedbackController.runAgent");
            var arrow = split.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_SecondaryButton");
            await c.KeyAsync(Key.F12, target: notes);
            await UseControlAsync(c, notes, arrow);
            var option = Control<MenuItem>(notes, "FeedbackController.runClaudeCode");
            await CheckHighlightAsync(c, notes, option, "feedback-agent-flyout");
            await c.ClickAsync(option, target: notes);
            Check(c.Window.Feedback.ActiveComment is { } comment && Text(comment, "Comment on: Refinement list: Run Claude Code").IsVisible,
                "The flyout item opens a comment with its caption");
            Equal(0, c.Launcher.Requests.Count, "Reporting the flyout item does not launch the agent");
        }),
        new("feedback-popup-command-completion", "Command completion entries can be reported while keeping the draft", async c =>
        {
            c.AddFile("completion candidate.txt", "");
            await c.KeyAsync(Key.L, RawInputModifiers.Control);
            await c.TextAsync("tool completion");
            await c.KeyAsync(Key.Space, RawInputModifiers.Control);
            var completion = c.Window.CommandLine.Completions;
            Check(completion.IsOpen && completion.Items.ItemCount == 1, "The command argument has one path suggestion");
            var suggestion = completion.Items.GetVisualDescendants().OfType<ListBoxItem>().Single();
            await c.KeyAsync(Key.F12);
            await CheckHighlightAsync(c, c.Window, suggestion, "feedback-command-completion");
            await c.ClickAsync(suggestion);
            Check(c.Window.Feedback.ActiveComment != null, "A suggestion opens a comment");
            Equal("tool completion", c.Window.CommandLine.Editor.Text, "Reporting a suggestion leaves the draft alone");
        }),
        new("feedback-control-click-runs-one-click", "UC39; use one normal click during feedback and keep the action path", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.F3);
            var viewer = c.Window.Viewer!;
            await c.KeyAsync(Key.F12, target: viewer);
            await UseControlAsync(c, viewer, (MenuItem)viewer.Reader.Commands.Items[1]!);
            await UseControlAsync(c, viewer, ViewerProbe.Command(viewer, "viewer.wrap"));
            Equal(TextWrapping.NoWrap, viewer.Reader.TextContent.TextWrapping, "The forwarded click runs the control exactly once");
            Check(c.Window.Feedback.IsPickingIn(viewer) && c.Window.Feedback.ActiveComment == null, "Feedback remains active after using a control");
            await c.ClickAsync(viewer.Reader.TextContent, target: viewer);
            var comment = c.Window.Feedback.ActiveComment!;
            await c.TextAsync("Keep the reading view clear", comment); await c.KeyAsync(Key.Enter, RawInputModifiers.Control, comment);
            var note = c.Window.Feedback.Notes.Last();
            Check(note.Target.Id.StartsWith("FileViewerWindow", StringComparison.Ordinal), "The note identifies the viewer source definition");
            Check(!IssueStore.Serialize(note).Contains("interactions", StringComparison.OrdinalIgnoreCase), "Transient click history is not stored");
        }),
        new("feedback-agent-selection", "The refinement list launches and remembers the selected console agent", async c =>
        {
            using var tools = WithAgentTools(c.FixturePath);
            var store = new SessionStore(Path.Combine(c.FixturePath, "workspace.json"));
            using var lifecycle = new WorkspaceLifecycle(c.Window, store, null);
            var showNotes = WorkspaceProbe.Button(c.Window, "ShowNotes");
            await c.ClickAsync(showNotes);
            var notes = c.Window.Feedback.ActiveNotes!;
            var button = Control<SplitButton>(notes, "FeedbackController.runAgent");
            Equal("Run Codex", button.Content, "Codex is the initial agent");
            await c.ClickAsync(button.GetVisualDescendants().OfType<Button>().Single(item => item.Name == "PART_SecondaryButton"), target: notes);
            await c.ClickAsync(Control<MenuItem>(notes, "FeedbackController.runClaudeCode"), target: notes);
            Equal("Run Claude Code", button.Content, "The main button follows the last choice");
            var launch = c.Launcher.Requests.Single();
            Equal("claude", Path.GetFileNameWithoutExtension(launch.Target), "The selected console application starts");
            Equal(c.FixturePath, launch.WorkingDirectory, "The console starts at the repository that owns the issues");
            Equal(CommandLine.FormatArgument(FeedbackAgentLauncher.Prompt), launch.Arguments, "The prompt asks for one related batch");
            await c.ScreenshotAsync("feedback-agent-choice", notes);
            await c.KeyAsync(Key.Escape, target: notes);

            var saved = await store.ReadAsync();
            Equal(FeedbackAgent.ClaudeCode, saved!.FeedbackAgent, "The choice is saved immediately");
            await c.Window.RestoreWorkspaceAsync(saved);
            await c.ClickAsync(showNotes);
            notes = c.Window.Feedback.ActiveNotes!;
            button = Control<SplitButton>(notes, "FeedbackController.runAgent");
            await c.ClickAsync(button.GetVisualDescendants().OfType<Button>().Single(item => item.Name == "PART_PrimaryButton"), target: notes);
            Equal("claude", Path.GetFileNameWithoutExtension(c.Launcher.Requests.Last().Target), "The main button launches the remembered agent");
        })
    ];
}
