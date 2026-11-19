// @implements RecognizeFileTypes, MeasureFolders, RefreshEntries, JumpToName, ClipboardTransfer, RestoreWorkspace
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop;

internal sealed class PanelRows(FileTypes fileTypes)
{
    private readonly Dictionary<string, DirectoryMeasurement> measurements = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> cutPaths = new(StringComparer.OrdinalIgnoreCase);
    private NameJump? nameSearch;
    public List<FileRow> Items { get; private set; } = [];

    public void Replace(List<FileRow> rows, bool showMeasurements)
    {
        Items = rows;
        foreach (var row in rows)
        {
            ApplyFileColor(row);
            if (showMeasurements) row.SetMeasurement(measurements.GetValueOrDefault(row.Name));
            row.SetCut(cutPaths.Contains(row.Entry.FullPath));
        }
    }

    public void SetCutPaths(IEnumerable<string> paths)
    {
        cutPaths = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Items) row.SetCut(cutPaths.Contains(row.Entry.FullPath));
    }

    public void SetFileTypes(FileTypes types)
    {
        fileTypes = types;
        foreach (var row in Items) ApplyFileColor(row);
    }

    private void ApplyFileColor(FileRow row) => row.SetFileColor(fileTypes.Match(row.Name, row.Entry.IsDirectory) is { } rule
        ? Brush.Parse(rule.Color) : Brushes.White);

    public void ShowNameMatches(NameJump? search)
    {
        nameSearch = search;
        RefreshNameMatches();
    }

    public void RefreshNameMatches()
    {
        foreach (var row in Items)
        {
            var match = row.Entry.IsParent ? null : nameSearch?.Match(row.Name);
            row.SetNameMatch(match?.Start ?? 0, match?.Length ?? 0);
        }
    }

    public bool SetMeasurement(DirectoryMeasurement measurement)
    {
        var row = Items.FirstOrDefault(row => row.Name.Equals(measurement.Name, StringComparison.OrdinalIgnoreCase) &&
            row.Entry.IsDirectory && !row.Entry.IsParent);
        if (row == null) return false;
        measurements[row.Name] = measurement;
        row.SetMeasurement(measurement);
        return true;
    }

    public void ClearMeasurements() => measurements.Clear();
    public DirectoryMeasurement[] CaptureMeasurements() => measurements.Values.ToArray();

    public void RestoreMeasurements(DirectoryMeasurement[]? saved)
    {
        foreach (var measurement in saved ?? []) measurements[measurement.Name] = measurement;
    }

    public void RetainMeasurements(IReadOnlyList<FileEntry> entries)
    {
        var previous = Items.ToDictionary(row => row.Entry.FullPath, row => row.Entry, StringComparer.OrdinalIgnoreCase);
        var unchanged = entries.Where(entry => previous.TryGetValue(entry.FullPath, out var old) && CanKeepMeasurement(entry, old))
            .Select(entry => entry.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in measurements.Keys.Where(name => !unchanged.Contains(name)).ToArray()) measurements.Remove(name);
    }

    private bool CanKeepMeasurement(FileEntry entry, FileEntry previous)
    {
        if (entry == previous) return true;
        // Windows can refresh a directory's cached timestamp after its contents were measured.
        return measurements.TryGetValue(entry.Name, out var measurement) && entry.ModifiedUtc <= measurement.MeasuredAt.UtcDateTime &&
            entry with { ModifiedUtc = previous.ModifiedUtc } == previous;
    }
}
