using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed class FileRow(FileEntry entry) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private bool isEditing;
    private string draftName = entry.Name;
    private IBrush fileColor = Brushes.White;
    private bool selected;
    public IBrush NameForeground => selected ? Brushes.White : fileColor;
    public void SetFileColor(IBrush color) { fileColor = color; Changed(nameof(NameForeground)); }
    public void SetSelected(bool value) { if (selected == value) return; selected = value; Changed(nameof(NameForeground)); }
    public FileEntry Entry { get; } = entry;
    public string Name => Entry.Name;
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
