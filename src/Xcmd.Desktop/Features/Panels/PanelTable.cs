// @implements SharedControls, SortEntries
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Xcmd.Desktop.Features.Panels;

internal sealed class PanelTable : TableView
{
    protected override Type StyleKeyOverride => typeof(TableView);
    internal Func<int> CurrentIndex { get; set; }
    internal Action<int, Key, bool> MoveTo { get; set; }
    internal event Action? RowOpened;

    internal PanelTable(bool fileRows = false)
    {
        Focusable = true;
        IsTextSearchEnabled = false;
        AutoScrollToSelectedItem = false;
        ScrollViewer.SetAllowAutoHide(this, false);
        ScrollViewer.SetBringIntoViewOnFocusChange(this, false);
        ScrollViewer.SetHorizontalScrollBarVisibility(this, ScrollBarVisibility.Auto);
        UiLayout.Table(this, panel: true);
        ItemContainerTheme = PanelRowTheme.Create(fileRows);
        if (!fileRows)
        {
            Styles.Add(new Style(s => s.OfType<TableViewRow>())
            { Setters = { new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent) } });
            Styles.Add(new Style(s => s.OfType<TableViewRow>().Class(":selected"))
            { Setters = { new Setter(TemplatedControl.BackgroundProperty, UiPalette.InactiveCursor) } });
            Styles.Add(new Style(s => s.OfType<TableView>().Class("active-panel").Descendant().OfType<TableViewRow>().Class(":selected"))
            { Setters = { new Setter(TemplatedControl.BackgroundProperty, UiPalette.Cursor) } });
        }
        CurrentIndex = () => SelectedIndex;
        MoveTo = (index, _, _) => { SelectedIndex = index; FocusRow(index); };
        DoubleTapped += (_, e) =>
        {
            if ((e.Source as Control)?.FindAncestorOfType<TableViewRow>(includeSelf: true) != null) RowOpened?.Invoke();
        };
    }

    internal static Button SortHeader(string title, HorizontalAlignment alignment = HorizontalAlignment.Left) =>
        new Button
        {
            Content = title,
            Focusable = false,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = alignment,
            FontSize = UiPalette.BodySize,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiPalette.Text,
            MinHeight = UiPalette.RowHeight
        }.WithFeedbackId("PanelView.button");

    internal static string SortCaption(string title, bool active, bool descending) =>
        active ? title + (descending ? "  ↓" : "  ↑") : title;

    internal bool HandleCursorKey(KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (e.Key is not (Key.Home or Key.End) && (ctrl || e.Key is not (Key.Up or Key.Down or Key.PageUp or Key.PageDown))) return false;
        MoveCursor(e.Key, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        return true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && HandleCursorKey(e)) e.Handled = true;
        else base.OnKeyDown(e);
    }

    internal void MoveCursor(Key key, bool extend)
    {
        if (ItemCount == 0) return;
        var current = Math.Max(0, CurrentIndex());
        var target = key switch
        {
            Key.Home => 0,
            Key.End => ItemCount - 1,
            Key.Up => current - 1,
            Key.Down => current + 1,
            Key.PageUp => current - PageSize(),
            Key.PageDown => current + PageSize(),
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };
        MoveTo(Math.Clamp(target, 0, ItemCount - 1), key, extend);
    }

    private int PageSize()
    {
        var height = GetRealizedContainers().FirstOrDefault()?.Bounds.Height ?? UiPalette.RowHeight;
        return Math.Max(1, (int)(Bounds.Height / Math.Max(1, height)) - 2);
    }

    internal void FocusRow(int index, bool scrollIntoView = true)
    {
        UpdateLayout();
        if (index >= 0 && index < ItemCount)
        {
            if (scrollIntoView) ScrollIntoView(index);
            UpdateLayout();
            if (ContainerFromIndex(index) is Control row) { row.Focus(); return; }
        }
        Focus();
    }
}
