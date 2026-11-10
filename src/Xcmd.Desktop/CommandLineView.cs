using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed class CommandLineView : Border
{
    private readonly TextBlock directory = new() { MaxWidth = 340, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly CommandHistory history = new();
    private bool browsing;
    public TextBox Editor { get; } = new()
    {
        Name = "CommandLine",
        Text = "",
        MaxLength = Core.CommandLine.MaximumLength,
        AcceptsReturn = false,
        PlaceholderText = "Run a program or file · cd changes this panel's directory",
        MinHeight = 28,
        Padding = new Thickness(5, 2)
    };
    public bool HasKeyboardFocus => Editor.IsKeyboardFocusWithin;
    public event Action? Focused;

    public CommandLineView()
    {
        Margin = new Thickness(0, 6, 0, 4);
        AutomationProperties.SetAutomationId(Editor, "command-line.input");
        AutomationProperties.SetName(Editor, "Command line");
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        directory.Margin = new Thickness(0, 0, 8, 0);
        layout.Children.Add(directory); Grid.SetColumn(Editor, 1); layout.Children.Add(Editor); Child = layout;
        ToolTip.SetTip(Editor, "Enter: run through Windows Shell in this directory · cd <path>: change directory · Esc: clear · Up/Down: files · Alt+Up/Down: command history · Ctrl+Enter: name · Ctrl+Shift+Enter: full path");
        Editor.GotFocus += (_, _) => Focused?.Invoke();
        Editor.TextChanging += (_, _) => { if (!browsing) history.Reset(); };
    }

    public void SetDirectory(string path) { directory.Text = path + ">"; ToolTip.SetTip(directory, path); }
    public void FocusCommand() => Editor.Focus();
    public void Clear() { Editor.Clear(); history.Reset(); }
    public void AddHistory(string command) => history.Add(command);
    public void BrowseHistory(bool older)
    {
        browsing = true;
        try { Editor.Text = history.Browse(Editor.Text ?? "", older); Editor.CaretIndex = Editor.Text.Length; }
        finally { browsing = false; }
        FocusCommand();
    }
    public void InsertText(string text)
    {
        Editor.SelectedText = text;
        FocusCommand();
    }
    public void InsertArgument(string value)
    {
        var text = Editor.Text ?? "";
        var start = Math.Min(Editor.SelectionStart, Editor.SelectionEnd);
        var separator = start > 0 && !char.IsWhiteSpace(text[start - 1]) ? " " : "";
        InsertText(separator + Core.CommandLine.FormatArgument(value) + " ");
    }
    public CommandLineState Capture() => new(Editor.Text ?? "", history.Capture(), HasKeyboardFocus,
        Editor.CaretIndex, Editor.SelectionStart, Editor.SelectionEnd);
    public void Restore(CommandLineState? state)
    {
        Editor.Text = state?.Draft ?? ""; history.Restore(state?.History ?? []);
        RestoreSelection(state);
    }
    public void RestoreSelection(CommandLineState? state)
    {
        var length = Editor.Text?.Length ?? 0;
        Editor.CaretIndex = Math.Clamp(state?.Caret ?? 0, 0, length);
        Editor.SelectionStart = Math.Clamp(state?.SelectionStart ?? 0, 0, length);
        Editor.SelectionEnd = Math.Clamp(state?.SelectionEnd ?? 0, 0, length);
    }
}
