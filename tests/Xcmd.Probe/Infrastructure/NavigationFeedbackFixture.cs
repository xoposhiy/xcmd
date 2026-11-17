// @implements HeadlessVerification, IntegrationVerification
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Xcmd.Probe.Infrastructure;

public static class NavigationFeedbackFixture
{
    public static void AddHistory(ProbeContext context)
    {
        var paths = Enumerable.Range(0, 50).Select(index => Path.Combine(context.LeftPath, "history-" + index.ToString("D2"))).ToArray();
        foreach (var path in paths) Directory.CreateDirectory(path);
        var state = context.Window.Left.CaptureState();
        context.Window.Left.Session.RestoreNavigation(state with { History = paths });
    }

    public static Color RowColor(ListBoxItem row) => ((ISolidColorBrush)row.GetVisualDescendants().OfType<ContentPresenter>().First().Background!).Color;

    public static async Task HoverAsync(ProbeContext context, Control row, Window window)
    {
        new InputDriver(window).MovePointer(row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value);
        await context.SettleAsync();
    }

    public static async Task AddReadOnlyRepositoryAsync(ProbeContext context)
    {
        var root = Path.Combine(context.LeftPath, "readonly-repository");
        Directory.CreateDirectory(Path.Combine(root, "folder1"));
        foreach (var name in new[] { "alpha.txt", "file2.log", "file10.bin", "zeta.md" }) File.WriteAllText(Path.Combine(root, name), "Git object fixture");
        File.WriteAllText(Path.Combine(root, "folder1", "inside.txt"), "Nested fixture");
        await GitHistoryFixture.CreateAsync(root);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, ".git", "objects"), "*", SearchOption.AllDirectories))
            File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);
    }
}

public sealed class LockedReadOnlyFile : IDisposable
{
    private readonly FileStream handle;
    public string Path { get; }

    public LockedReadOnlyFile(string directory)
    {
        Path = System.IO.Path.Combine(directory, "locked-readonly.txt");
        File.WriteAllText(Path, "Keep this file on failure");
        File.SetAttributes(Path, FileAttributes.ReadOnly | FileAttributes.Hidden);
        handle = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public void Dispose() { handle.Dispose(); File.SetAttributes(Path, FileAttributes.Normal); }
}
