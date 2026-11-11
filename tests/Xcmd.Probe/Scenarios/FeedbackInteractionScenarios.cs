using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.FeedbackProbe;

namespace Xcmd.Probe.Scenarios;

public static class FeedbackInteractionScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("feedback-double-click-menu-and-saved-context", "UC39; interact with menus while selecting feedback and save the click path", async c =>
        {
            var menu = c.Window.GetVisualDescendants().OfType<Menu>().Single();
            var file = (MenuItem)menu.Items[0]!;
            await c.KeyAsync(Key.F12);
            await DoubleClickAsync(c, c.Window, file);
            Check(c.Window.IsMenuOpen && c.Window.Feedback.IsPicking, "Double-click opens a menu without ending feedback mode");
            Check(c.Window.Feedback.ActiveComment == null, "Double-click does not open a premature comment");
            var rename = file.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == "Rename");
            await c.ClickAsync(rename);
            var comment = c.Window.Feedback.ActiveComment!;
            Check(comment != null && c.Window.ActivePanel.EditingRow == null, "Single-click selects a menu command for comment without running it");
            Check(Text(comment!, "Actions: File > Rename").IsVisible, "The comment form shows its action context");
            await c.TextAsync("Explain this command", comment); await c.KeyAsync(Key.Enter, RawInputModifiers.Control, comment);
            var note = c.Window.Feedback.Notes.Last();
            Equal("Rename", note.ElementName, "The note identifies the chosen menu item");
            Equal(2, note.Interactions!.Length, "The note stores the menu click and final selection");
            Equal("File", note.Interactions[0].ElementName, "The interaction path starts with the opened menu");
            Equal("Click", note.Interactions[0].Action, "A forwarded click is distinct from the final comment selection");
            Equal("Comment", note.Interactions[1].Action, "The path records the commented target");
            var store = new SessionStore(Path.Combine(c.FixturePath, "feedback-session.json"));
            await store.SaveAsync(c.Window.CaptureWorkspace());
            Equal("File", (await store.ReadAsync())!.Notes.Last().Interactions![0].ElementName, "The real session format preserves click context");
            var list = Path.Combine(c.FixturePath, "refinements.md");
            await RefinementList.SaveAsync(list, c.Window.Feedback.Notes);
            Check((await File.ReadAllTextAsync(list)).Contains("Click: File", StringComparison.Ordinal), "The review list includes the action path");
        }),
        new("feedback-double-click-runs-one-click", "UC39; use one normal click during feedback and keep the action path", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.F3);
            var viewer = c.Window.Viewer!;
            await c.KeyAsync(Key.F12, target: viewer);
            await DoubleClickAsync(c, viewer, (MenuItem)viewer.Commands.Items[1]!);
            await DoubleClickAsync(c, viewer, ViewerProbe.Command(viewer, "viewer.wrap"));
            Equal(TextWrapping.NoWrap, viewer.TextContent.TextWrapping, "The forwarded click runs the control exactly once");
            Check(c.Window.Feedback.IsPickingIn(viewer) && c.Window.Feedback.ActiveComment == null, "Feedback remains active after using a control");
            await c.ClickAsync(viewer.TextContent, target: viewer);
            var comment = c.Window.Feedback.ActiveComment!;
            await c.TextAsync("Keep the reading view clear", comment); await c.KeyAsync(Key.Enter, RawInputModifiers.Control, comment);
            var note = c.Window.Feedback.Notes.Last();
            Equal(3, note.Interactions!.Length, "The note stores both interactions before the final target");
            Equal("View", note.Interactions[0].ElementName, "The path includes the menu");
            Equal("Word wrap", note.Interactions[1].ElementName, "The path includes the command");
            Equal(c.LeftPath, note.PanelPath, "Viewer feedback identifies its source directory");
        })
    ];
}
