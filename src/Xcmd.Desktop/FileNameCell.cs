// @implements JumpToName, ReadablePanels, MarkEntries, RenameEntry, FileIcons
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Xcmd.Desktop;

internal static class FileNameCell
{
    public static Control Create(FileRow row, FileIconCache icons)
    {
        var grid = new Grid { DataContext = row, MinHeight = UiPalette.RowHeight, ColumnDefinitions = new ColumnDefinitions("16,24,*") }.WithFeedbackId("PanelView.grid");
        var mark = new TextBlock { Name = "FileMark", Foreground = UiPalette.Mark, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center }.WithFeedbackId("PanelView.mark");
        mark.Bind(TextBlock.TextProperty, new Binding(nameof(FileRow.MarkGlyph)));
        grid.Children.Add(mark);
        var icon = new Image { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, DataContext = row }.WithFeedbackId("PanelView.icon");
        AutomationProperties.SetAutomationId(icon, "file.icon");
        Grid.SetColumn(icon, 1); grid.Children.Add(icon); icons.Bind(icon, row);
        var text = new FileNameText
        {
            Text = row.Name,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = row.Entry.IsDirectory ? FontWeight.SemiBold : FontWeight.Normal
        }.WithFeedbackId("PanelView.text.2");
        text.Bind(Control.IsVisibleProperty, new Binding(nameof(FileRow.IsReading)));
        text.Bind(TextBlock.ForegroundProperty, new Binding(nameof(FileRow.NameForeground)));
        text.Bind(FileNameText.MatchStartProperty, new Binding(nameof(FileRow.NameMatchStart)));
        text.Bind(FileNameText.MatchLengthProperty, new Binding(nameof(FileRow.NameMatchLength)));
        var editor = new TextBox
        {
            Name = "RenameEditor",
            MinHeight = UiPalette.RowHeight,
            Padding = new Thickness(3, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = UiPalette.BodySize,
            MaxLength = 255
        }.WithFeedbackId("PanelView.editor");
        AutomationProperties.SetAutomationId(editor, "rename.editor");
        editor.Bind(Control.IsVisibleProperty, new Binding(nameof(FileRow.IsEditing)));
        editor.Bind(TextBox.TextProperty, new Binding(nameof(FileRow.DraftName)) { Mode = BindingMode.TwoWay });
        // Stop boundary navigation reaching the table; printable keys still reach text input.
        editor.KeyDown += (_, e) => { if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown) e.Handled = true; };
        Grid.SetColumn(text, 2); grid.Children.Add(text);
        Grid.SetColumn(editor, 2); grid.Children.Add(editor);
        return grid;
    }
}
