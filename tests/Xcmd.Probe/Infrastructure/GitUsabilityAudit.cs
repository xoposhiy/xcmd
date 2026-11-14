using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;

namespace Xcmd.Probe.Infrastructure;

public static class GitUsabilityAudit
{
    public static async Task<Dictionary<string, object?>> CollectAsync(ProbeContext context)
    {
        using var fixture = await GitHistoryFixture.CreateAsync(context.LeftPath);
        var result = new Dictionary<string, object?>();
        var window = context.Window;
        await context.SelectAsync("..");
        await context.KeyAsync(Key.L, RawInputModifiers.Control | RawInputModifiers.Alt);
        var panel = window.Right;
        result["logFooter"] = GitHistoryProbe.Summary(panel);
        var beforeSort = panel.Rows.Select(row => row.Name).ToArray();
        await context.KeyAsync(Key.F6, RawInputModifiers.Control);
        result["logSort"] = new
        {
            namesUnchanged = beforeSort.SequenceEqual(panel.Rows.Select(row => row.Name)),
            followingSnapshotSort = panel.Session.SortOrder.Field.ToString()
        };
        await GitHistoryProbe.SelectCommitAsync(context, panel, "Update root files");
        await context.KeyAsync(Key.Enter);
        result["snapshotDates"] = panel.Rows.Where(row => !row.Entry.IsDirectory).Select(row => new
        { row.Name, row.DisplayDate, row.Git.HasChanges }).ToArray();
        result["gitColumnLegend"] = ToolTip.GetTip((Control)panel.Table.Columns[3].Header!);
        await context.SelectAsync("alpha.txt");
        await context.KeyAsync(Key.C, RawInputModifiers.Control);
        result["snapshotCopyMessage"] = window.LastError;
        await context.KeyAsync(Key.F5);
        result["suggestedCopyCommandMessage"] = window.LastError;
        await context.KeyAsync(Key.F3);
        var viewer = window.Viewer!;
        var launches = context.Launcher.Requests.Count;
        result["snapshotViewerPath"] = viewer.CurrentPath;
        result["snapshotViewerTitle"] = viewer.Title;
        await context.KeyAsync(Key.F4, target: viewer);
        result["snapshotViewerEdit"] = new
        {
            opened = context.Launcher.Requests.Count > launches,
            request = context.Launcher.Requests.LastOrDefault(),
            window.LastError,
            viewer.Error
        };
        await context.ScreenshotAsync("snapshot-viewer", viewer);
        viewer.Close(); await context.SettleAsync(); panel.FocusCurrent();
        await context.SelectAsync(".."); await context.KeyAsync(Key.F3);
        result["actionableChangedFiles"] = panel.Rows.Where(row => !row.Entry.IsParent).Select(row => new { row.Name, row.Git.WorkingGlyph }).ToArray();
        await context.ScreenshotAsync("changed-files");
        await context.SelectAsync("zeta.md"); await context.KeyAsync(Key.D, RawInputModifiers.Control | RawInputModifiers.Alt);
        result["deletedCommitDiff"] = new { window.Viewer!.Title, text = window.Viewer.TextContent.Text };
        await context.ScreenshotAsync("deleted-file-diff", window.Viewer);
        window.Viewer.Close(); await context.SettleAsync(); panel.FocusCurrent(); await context.KeyAsync(Key.Back);
        await context.SelectAsync("folder1");
        await context.KeyAsync(Key.Left, RawInputModifiers.Control);
        await context.KeyAsync(Key.Tab);
        var beforeEscape = window.Left.Session.DirectoryPath;
        await context.KeyAsync(Key.Escape);
        result["crossPanelEscape"] = new
        {
            beforeEscape,
            afterEscape = window.Left.Session.DirectoryPath,
            expectedPhysicalDirectory = context.LeftPath,
            remainedInGit = window.Left.HasHistory
        };
        await context.ScreenshotAsync("cross-panel-escape");
        await window.Right.ExitHistoryAsync();
        await window.Left.LoadAsync(context.LeftPath);
        var empty = Path.Combine(context.LeftPath, "empty");
        Directory.CreateDirectory(empty);
        File.WriteAllText(Path.Combine(empty, "ignored.txt"), "Ignored contents");
        File.AppendAllText(Path.Combine(context.LeftPath, ".git", "info", "exclude"), "\n/empty/\n");
        await window.Left.LoadAsync(empty);
        window.Activate(window.Left);
        window.GitHistory.Open(selectedItem: false);
        await context.SettleAsync();
        result["emptyHistory"] = new
        {
            rows = panel.Rows.Select(row => row.Name).ToArray(),
            message = panel.CommitMessage.Text,
            panel.DisplayPath
        };
        await context.ScreenshotAsync("empty-history");
        await panel.ExitHistoryAsync();
        await window.Left.LoadAsync(context.LeftPath);
        File.AppendAllText(Path.Combine(context.LeftPath, "alpha.txt"), "Working change");
        window.Activate(window.Left); window.GitHistory.Open(selectedItem: false);
        await context.SettleAsync(); await context.KeyAsync(Key.Enter);
        var capturedRows = panel.Rows.Select(row => row.Name).ToArray();
        File.WriteAllText(Path.Combine(context.LeftPath, "new-after-open.txt"), "New untracked file");
        await context.SelectAsync(".."); await context.KeyAsync(Key.F3);
        result["workingTreeFreshness"] = new
        {
            capturedRows,
            currentRows = panel.Rows.Select(row => row.Name).ToArray(),
            refreshedReportIncludesNewFile = window.Viewer!.TextContent.Text!.Contains("new-after-open.txt", StringComparison.Ordinal)
        };
        window.Viewer.Close(); await context.SettleAsync();
        await context.ScreenshotAsync("working-tree-refreshed");
        context.SaveSnapshot();
        return result;
    }
}
