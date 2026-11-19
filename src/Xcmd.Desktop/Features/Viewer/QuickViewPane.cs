// @implements ReusableFileViewer, QuickViewFile, ReportControl
using Avalonia.Controls;
using Xcmd.Core;
using Xcmd.Desktop.Features.Panels;

namespace Xcmd.Desktop.Features.Viewer;

internal sealed class QuickViewPane : Border
{
    private PanelView? source;
    private PanelView? destination;
    public FileViewer? Reader { get; private set; }

    public QuickViewPane()
    {
        Name = "QuickViewPane";
        IsVisible = false;
        ClipToBounds = true;
    }

    public void ShowPreview(PanelView active, PanelView other, FileViewer reader)
    {
        source = active;
        destination = other;
        Reader = reader;
        Child = reader;
        Grid.SetColumn(this, Grid.GetColumn(other));
        other.IsVisible = false;
        IsVisible = true;
        active.CurrentEntryChanged += Refresh;
        Refresh();
    }

    public bool HidePreview()
    {
        if (source == null || destination == null) return false;
        source.CurrentEntryChanged -= Refresh;
        Reader?.Stop();
        Reader = null;
        Child = null;
        destination.IsVisible = true;
        IsVisible = false;
        source = destination = null;
        return true;
    }

    public void CloseFromReader()
    {
        var active = source;
        if (HidePreview()) active?.FocusCurrent(scrollIntoView: false);
    }

    public void SwitchFocus()
    {
        if (IsKeyboardFocusWithin) FocusSource();
        else Reader?.FocusReader();
    }

    public void FocusSource()
    {
        Reader?.ResetKeys();
        source?.FocusCurrent(scrollIntoView: false);
    }

    private void Refresh()
    {
        if (source?.HasHistory == true) { HidePreview(); return; }
        Reader?.Preview(source?.Current?.Entry is { IsDirectory: false } entry ? entry.FullPath : null);
    }
}
