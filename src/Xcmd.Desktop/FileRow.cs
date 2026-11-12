using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed class FileRow(FileEntry entry, string? resultName = null) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private bool isEditing;
    private string draftName = entry.Name;
    private IBrush fileColor = Brushes.White;
    private bool current;
    private bool active;
    private bool marked;
    private bool cut;
    public bool IsCut => cut;
    public double ContentOpacity => cut ? 0.5 : 1;
    public void SetCut(bool value)
    {
        if (cut == value) return;
        cut = value; Changed(nameof(IsCut)); Changed(nameof(ContentOpacity));
    }
    public int NameMatchStart { get; private set; }
    public int NameMatchLength { get; private set; }
    public void SetNameMatch(int start, int length)
    {
        if (NameMatchStart == start && NameMatchLength == length) return;
        NameMatchStart = start; NameMatchLength = length;
        Changed(nameof(NameMatchStart)); Changed(nameof(NameMatchLength));
    }
    private static readonly IBrush markColor = UiPalette.Mark;
    public bool IsMarked => marked;
    public string MarkGlyph => marked ? "✓" : "";
    private static readonly IBrush activeCursor = UiPalette.Cursor;
    private static readonly IBrush inactiveCursor = UiPalette.InactiveCursor;
    public bool IsCurrent => current;
    public IBrush RowBackground => current ? active ? activeCursor : inactiveCursor : Brushes.Transparent;
    public IBrush NameForeground => marked ? markColor : current ? Brushes.White : fileColor;
    public void SetMarked(bool value)
    {
        if (marked == value) return;
        marked = value; Changed(nameof(IsMarked)); Changed(nameof(MarkGlyph)); Changed(nameof(NameForeground));
    }
    public void SetFileColor(IBrush color) { fileColor = color; Changed(nameof(NameForeground)); }
    public void SetCurrent(bool value, bool panelActive)
    {
        if (current == value && active == panelActive) return;
        current = value; active = panelActive;
        Changed(nameof(IsCurrent)); Changed(nameof(RowBackground)); Changed(nameof(NameForeground));
    }
    public FileEntry Entry { get; } = entry;
    public string Name => resultName ?? Entry.Name;
    public string Kind => Entry.IsDirectory ? "DIR" : Entry.Extension.ToUpperInvariant();
    public DirectoryMeasurement? Measurement { get; private set; }
    private string EntrySize => Entry.IsParent ? "<UP>" : Entry.IsDirectory ? "<DIR>" : Entry.Size.ToString("N0");
    public string DisplaySize => Measurement is { } measurement ? (measurement.Size.Incomplete ? "≥ " : "") + measurement.Size.Bytes.ToString("N0") : EntrySize;
    public string SizeDescription => Measurement is { } measurement
        ? $"Measured {measurement.MeasuredAt.ToLocalTime():yyyy-MM-dd HH:mm:ss} · Files: {measurement.Size.Files:N0} · Links skipped: {measurement.Size.SkippedLinks:N0} · Unreadable directories: {measurement.Size.Errors:N0}"
        : Entry.IsDirectory ? "Alt+Shift+Enter calculates folder sizes." : EntrySize + " bytes";
    public void SetMeasurement(DirectoryMeasurement? measurement) { Measurement = measurement; Changed(nameof(DisplaySize)); Changed(nameof(SizeDescription)); }
    public string DisplayDate => Entry.IsParent ? "" : Entry.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public bool IsEditing
    {
        get => isEditing;
        set { isEditing = value; Changed(); Changed(nameof(IsReading)); }
    }
    public bool IsReading => !IsEditing;
    public string DraftName
    {
        get => draftName;
        set { draftName = value; Changed(); }
    }
}
