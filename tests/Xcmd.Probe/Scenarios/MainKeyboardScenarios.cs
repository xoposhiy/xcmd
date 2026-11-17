using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class MainKeyboardScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("main-keyboard-git-button", "Typing and Tab work after Git actions close or leave focus on the repository button", async c =>
        {
            await GitFixture.InitializeAsync(c.RightPath);
            try
            {
                await GitFixture.RefreshAsync(c.Window.Right.Git);
                await c.ClickAsync(c.Window.Right.Git.ActionsButton);
                await c.KeyAsync(Key.Escape);
                c.Window.Right.Git.ActionsButton.Focus();
                Check(c.Window.Right.Git.ActionsButton.IsKeyboardFocusWithin, "The reported button focus state is reproduced");
                await c.TextAsync("git status");
                Equal("git status", c.Window.CommandLine.Editor.Text, "Typing from the Git button reaches the command line once");
                Check(c.Window.CommandLine.HasKeyboardFocus, "Typing moves focus into the command line");
                Equal(c.Window.Right, c.Window.ActivePanel, "Typing keeps the repository's active panel");
                await c.KeyAsync(Key.Escape);
                foreach (var modifiers in new[] { RawInputModifiers.None, RawInputModifiers.Shift })
                {
                    c.Window.Activate(c.Window.Right);
                    c.Window.Right.Git.ActionsButton.Focus();
                    await c.KeyAsync(Key.Tab, modifiers);
                    Check(c.Window.ActivePanel == c.Window.Left && c.Window.Left.Table.IsKeyboardFocusWithin, "Tab switches panels instead of visiting controls");
                }
            }
            finally { GitFixture.AllowCleanup(c.RightPath); }
        }),
        new("main-keyboard-other-controls", "Main-window buttons do not trap typing or panel switching", async c =>
        {
            var button = c.Window.GetVisualDescendants().OfType<Button>().Single(control => control.Name == "StartFeedback");
            button.Focus();
            await c.TextAsync("echo 日本語");
            Equal("echo 日本語", c.Window.CommandLine.Editor.Text, "Text from a footer button reaches the command line");
            await c.KeyAsync(Key.Escape);
            button.Focus();
            await c.KeyAsync(Key.Tab);
            Check(c.Window.ActivePanel == c.Window.Right && c.Window.Right.Table.IsKeyboardFocusWithin, "Tab from a footer button switches panels");
            await PriorityBatchProbe.CopyAsync(c, "alpha.txt");
            c.Window.OperationsView.ToggleDetails();
            await c.SettleAsync();
            Check(c.Window.OperationsView.IsKeyboardFocusWithin, "The operation list has keyboard focus");
            await c.KeyAsync(Key.Tab, RawInputModifiers.Shift);
            Check(c.Window.ActivePanel == c.Window.Left && c.Window.Left.Table.IsKeyboardFocusWithin, "Shift+Tab leaves operation controls for the other panel");
        })
    ];
}
