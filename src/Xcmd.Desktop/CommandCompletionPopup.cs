// @implements CompleteCommand
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed class CommandCompletionPopup
{
    private readonly TextBox editor;
    private readonly Popup popup;
    public ListBox Items { get; } = new ListBox() { Name = "CommandCompletionItems", MaxHeight = 240, IsTextSearchEnabled = false }.WithFeedbackId("CommandCompletionPopup.Items");
    private CancellationTokenSource? request;
    private string original = "";
    private CommandToken? token;
    private CommandSuggestion[] suggestions = [];
    public bool IsOpen => popup.IsOpen;

    public CommandCompletionPopup(TextBox editor)
    {
        this.editor = editor;
        AutomationProperties.SetAutomationId(Items, "command-line.completions");
        popup = new Popup
        {
            PlacementTarget = editor,
            Placement = PlacementMode.Top,
            IsLightDismissEnabled = true,
            Child = new Border
            {
                Background = UiPalette.Surface,
                BorderBrush = UiPalette.ControlBorder,
                BorderThickness = new Avalonia.Thickness(1),
                Child = Items
            }.WithFeedbackId("CommandCompletionPopup.content")
        }.WithFeedbackId("CommandCompletionPopup.content.2");
        editor.TextChanging += (_, _) => Close();
        editor.DetachedFromVisualTree += (_, _) => Close();
        Items.AddHandler(InputElement.PointerReleasedEvent, (_, _) => Accept(), RoutingStrategies.Bubble);
    }

    public async Task OpenAsync(string directory)
    {
        Close();
        using var owner = new CancellationTokenSource(); request = owner;
        var draft = editor.Text ?? ""; var caret = editor.CaretIndex;
        var part = CommandToken.At(draft, caret);
        try
        {
            var found = await CommandCompletions.FindAsync(part, directory, owner.Token);
            if (owner.IsCancellationRequested || draft != editor.Text || caret != editor.CaretIndex || !editor.IsKeyboardFocusWithin) return;
            suggestions = found; token = part; original = draft;
            Items.ItemsSource = found.Select(item => item.Value).ToArray(); Items.SelectedIndex = found.Length == 0 ? -1 : 0;
            popup.Width = Math.Max(300, editor.Bounds.Width); popup.IsOpen = found.Length > 0;
        }
        catch (OperationCanceledException) when (owner.IsCancellationRequested) { }
        finally { if (request == owner) request = null; }
    }

    public void Close() { request?.Cancel(); popup.IsOpen = false; }

    public bool HandleKey(KeyEventArgs e)
    {
        if (!IsOpen) return false;
        if (e.Key is Key.Up or Key.Down && e.KeyModifiers == KeyModifiers.None)
        { Items.SelectedIndex = Math.Clamp(Items.SelectedIndex + (e.Key == Key.Down ? 1 : -1), 0, suggestions.Length - 1); Items.ScrollIntoView(Items.SelectedItem!); }
        else if (e.Key == Key.Enter) Accept();
        else if (e.Key == Key.Escape) Close();
        else { Close(); return false; }
        return true;
    }

    private void Accept()
    {
        if (token == null || Items.SelectedIndex < 0) return;
        var value = suggestions[Items.SelectedIndex].Value;
        var text = token.Replace(original, value);
        var caret = token.Start + Core.CommandLine.FormatArgument(value).Length;
        Close(); editor.Text = text; editor.CaretIndex = caret; editor.Focus();
    }
}
