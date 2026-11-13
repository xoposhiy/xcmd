using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Core;

namespace Xcmd.Desktop;

internal sealed class QueryHistoryPopup
{
    private readonly TextBox editor;
    private readonly CommandHistory history = new();
    private readonly ListBox list = new ListBox() { Name = "QueryHistoryItems", MaxHeight = 220, IsTextSearchEnabled = false }.WithFeedbackId("QueryHistoryPopup.list");
    public Popup Popup { get; }

    public QueryHistoryPopup(TextBox editor, string[]? entries)
    {
        this.editor = editor;
        history.Restore(entries ?? []);
        Popup = new Popup
        {
            Name = editor.Name + "History",
            PlacementTarget = editor,
            Placement = PlacementMode.Bottom,
            IsLightDismissEnabled = true,
            Child = new Border { Background = UiPalette.Surface, BorderBrush = UiPalette.ControlBorder, BorderThickness = new Thickness(1), Child = list }.WithFeedbackId("QueryHistoryPopup.content")
        }.WithFeedbackId("QueryHistoryPopup.content.2");
        list.AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
        { if (e.Source is Control control && control.GetSelfAndVisualAncestors().OfType<ListBoxItem>().Any()) Accept(); }, RoutingStrategies.Bubble);
        ToolTip.SetTip(editor, "Alt+Down: query history · Up/Down: switch query fields");
    }

    public void Add(string text) { if (!string.IsNullOrWhiteSpace(text)) history.Add(text); }
    public string[] Capture() => history.Capture();
    public void Close() => Popup.IsOpen = false;

    public bool HandleKey(KeyEventArgs e)
    {
        if (e.Key == Key.Down && e.KeyModifiers == KeyModifiers.Alt && editor.IsKeyboardFocusWithin)
        {
            var entries = history.Capture().Reverse().ToArray();
            list.ItemsSource = entries; list.SelectedIndex = entries.Length > 0 ? 0 : -1;
            Popup.Width = Math.Max(260, editor.Bounds.Width); Popup.IsOpen = entries.Length > 0;
            return true;
        }
        if (!Popup.IsOpen) return false;
        if (e.Key is Key.Up or Key.Down)
        { list.SelectedIndex = Math.Clamp(list.SelectedIndex + (e.Key == Key.Down ? 1 : -1), 0, list.ItemCount - 1); list.ScrollIntoView(list.SelectedItem!); }
        else if (e.Key == Key.Enter) Accept();
        else if (e.Key == Key.Escape) { Close(); editor.Focus(); }
        else { Close(); return false; }
        return true;
    }

    private void Accept()
    {
        if (list.SelectedItem is string text) { editor.Text = text; editor.Focus(); editor.SelectAll(); }
        Close();
    }
}
