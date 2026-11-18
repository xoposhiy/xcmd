// @tests ReportControl, KeepFeedback, FeedbackIdentity, FeedbackQueue
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
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
            await UseControlAsync(c, c.Window, file);
            Check(c.Window.IsMenuOpen && c.Window.Feedback.IsPicking, "Ctrl+click opens a menu without ending feedback mode");
            Check(c.Window.Feedback.ActiveComment == null, "Ctrl+click does not open a premature comment");
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
        })
    ];
}
