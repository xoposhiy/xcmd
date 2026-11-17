// @implements MeasureFolders
using Avalonia.Input;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed partial class CommanderWindow
{
    private CancellationTokenSource? directorySizes;
    private PanelView? sizePanel;
    public bool IsCalculatingDirectorySizes => directorySizes != null;
    private void CancelDirectorySizes() => directorySizes?.Cancel();

    private bool HandleDirectorySizeShortcut(KeyEventArgs e, bool shift)
    {
        if (e.Key == Key.Escape && directorySizes != null) CancelDirectorySizes();
        else if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.None && ActivePanel.Current is { Entry: { IsDirectory: true, IsParent: false } } row)
        {
            var panel = ActivePanel;
            if (launchKeysHeld.Add(e.Key) && directorySizes == null) Execute(() => MeasureDirectoriesAsync(panel, [row.Entry]));
        }
        else if (e.Key == Key.Enter && altInput && shift) Execute(CalculateDirectorySizesAsync);
        else return false;
        return true;
    }

    private async Task CalculateDirectorySizesAsync()
    {
        if (directorySizes != null) { CancelDirectorySizes(); return; }
        var panel = ActivePanel;
        var entries = panel.Rows.Select(row => row.Entry).Where(entry => entry.IsDirectory && !entry.IsParent).ToArray();
        if (entries.Length == 0) { status.Text = "No child directories to measure."; return; }
        await MeasureDirectoriesAsync(panel, entries);
    }

    private async Task MeasureDirectoriesAsync(PanelView panel, FileEntry[] entries)
    {
        var directory = panel.Session.DirectoryPath;
        using var cancellation = new CancellationTokenSource();
        directorySizes = cancellation; sizePanel = panel;
        var calculator = new DirectorySizeCalculator(panel.Session.FileSystem);
        var completed = 0; var incomplete = 0; var links = 0;
        try
        {
            foreach (var entry in entries)
            {
                status.Text = $"Folder sizes: {completed}/{entries.Length} · {entry.Name} · Esc cancels";
                var result = entry.IsLink ? new DirectorySize(0, 0, 1, 0) : await calculator.CalculateAsync(entry.FullPath, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (panel.Session.DirectoryPath != directory) return;
                panel.SetDirectorySize(new(entry.Name, result, DateTimeOffset.UtcNow));
                completed++; if (result.Incomplete) incomplete++; links += result.SkippedLinks;
            }
            status.Text = $"Folder sizes: {completed} calculated · {incomplete} incomplete · {links} links skipped";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { status.Text = "Folder size calculation canceled."; }
        finally { directorySizes = null; sizePanel = null; }
    }
}
