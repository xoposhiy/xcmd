// @implements RunCommand, CommandHistory, CompleteCommand, ReusePaths, RestoreWorkspace
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed class CommandLineView : Border
{
    private readonly TextBlock directory = new TextBlock() { MaxWidth = 510, TextTrimming = TextTrimming.LeadingCharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }.WithFeedbackId("CommandLineView.directory");
    private readonly CommandHistory history = new();
    private bool browsing;
    public TextBox Editor { get; } = new TextBox()
    {
        Name = "CommandLine",
        Text = "",
        MaxLength = Core.CommandLine.MaximumLength,
        AcceptsReturn = false,
        PlaceholderText = "Command",
        MinHeight = UiPalette.ControlHeight,
        Padding = new Thickness(5, 2)
    }.WithFeedbackId("CommandLineView.Editor");
    public bool HasKeyboardFocus => Editor.IsKeyboardFocusWithin;
    internal bool HasFocusedDraft => HasKeyboardFocus && !string.IsNullOrWhiteSpace(Editor.Text);
    public CommandCompletionPopup Completions { get; }
    private string currentDirectory = "";
    public Task CompleteAsync() => Completions.OpenAsync(currentDirectory);
    public event Action? Focused;

    public CommandLineView()
    {
        Completions = new CommandCompletionPopup(Editor);
        Margin = new Thickness(0, 6, 0, 4);
        AutomationProperties.SetAutomationId(Editor, "command-line.input");
        AutomationProperties.SetName(Editor, "Command line");
        AutomationProperties.SetAutomationId(directory, "command-line.directory");
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") }.WithFeedbackId("CommandLineView.layout");
        directory.Margin = new Thickness(0, 0, 8, 0);
        layout.Children.Add(directory); Grid.SetColumn(Editor, 1); layout.Children.Add(Editor);
        layout.Children.Add(Completions.Popup);
        Child = layout;
        ToolTip.SetTip(Editor, "Enter: run through Windows Shell in this directory · cd <path>: change directory · Esc: clear and return to files · Ctrl+C: copy selection or the whole command · Up/Down: files · Alt+Up/Down: command history · Ctrl+Enter: name · Ctrl+Shift+Enter: full path");
        SizeChanged += (_, _) => directory.MaxWidth = Math.Min(510, Math.Max(0, Bounds.Width - 328));
        Editor.GotFocus += (_, _) => Focused?.Invoke();
        Editor.TextChanging += (_, _) => { if (!browsing) history.Reset(); };
    }

    public void SetDirectory(string path)
    {
        currentDirectory = path;
        var display = PathText.Display(path);
        directory.Text = display + ">"; ToolTip.SetTip(directory, display);
    }
    public void FocusCommand() => Editor.Focus();
    public void Clear() { Editor.Clear(); history.Reset(); }
    public async Task CopyAllAsync()
    {
        if (string.IsNullOrEmpty(Editor.Text)) return;
        var clipboard = TopLevel.GetTopLevel(Editor)?.Clipboard ?? throw new IOException("The clipboard is unavailable.");
        await clipboard.SetTextAsync(Editor.Text);
    }
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
        var argument = Core.CommandLine.FormatArgument(value);
        InsertText(separator + argument + " ");
        Editor.SelectionStart = start + separator.Length;
        Editor.SelectionEnd = Editor.SelectionStart + argument.Length;
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
