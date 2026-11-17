using Avalonia;
using Avalonia.Input;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class FileDragScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("file-drag-selection-and-target", "Dragging keeps the selected files, supports both buttons and targets the folder under the pointer", async c =>
        {
            await c.SelectAsync("alpha.txt");
            await c.KeyAsync(Key.A, RawInputModifiers.Control);
            var selected = c.Window.Left.SelectedNames();
            c.FileDrag.DuringDrag = () => Check(c.Window.IsBusy, "A drag blocks automatic release handoff");
            FileDragProbe.Drag(c, "alpha.txt");
            Equal(selected.Length, c.FileDrag.Drags.Single().Paths.Length, "Dragging a marked row keeps the full selection");
            Check(selected.SequenceEqual(c.Window.Left.SelectedNames()), "Cancelling the drag preserves marks");
            await c.SettleAsync();
            FileDragProbe.Drag(c, "folder1", MouseButton.Right);
            Check(c.FileDrag.Drags.Last().Right, "Right-button drags reach the Windows source");
            Equal(0, c.Shell.Menus.Count, "A right-button drag does not open the source context menu");
            var target = c.DragDrop.DestinationAt(FileDragProbe.RowPoint(c, "folder2"));
            Equal(Path.Combine(c.LeftPath, "folder2"), target, "Dropping on a folder uses that folder");
            Check(c.Window.Left.Rows.Single(row => row.Name == "folder2").IsDropTarget, "The destination folder is highlighted");
            Equal(c.LeftPath, c.DragDrop.DestinationAt(FileDragProbe.RowPoint(c, "alpha.txt")), "An ordinary file row uses the current folder");
            Check(!c.Window.Left.Rows.Any(row => row.IsDropTarget), "Moving off the folder clears its highlight");
            Equal(c.RightPath, c.DragDrop.DestinationAt(c.Window.Right.Table.PointToScreen(new Point(100, 250))), "The other panel accepts files on its background");
            Equal<string?>(null, c.DragDrop.DestinationAt(c.Window.TitleBar.PointToScreen(new Point(10, 10))), "Window chrome rejects drops");
            await c.ScreenshotAsync("drag-selection");
        }),
        new("file-drag-native-shell", "The Windows Shell copies, moves and creates shortcuts for real files and folders without a visible application window", async c =>
        {
            await FileDragProbe.NativeAsync([Path.Combine(c.LeftPath, "alpha.txt"), Path.Combine(c.LeftPath, "folder1")], c.RightPath, 8, 1);
            Check(File.Exists(Path.Combine(c.LeftPath, "alpha.txt")), "Ctrl-drag preserves the source");
            Equal("abc", File.ReadAllText(Path.Combine(c.RightPath, "alpha.txt")), "Ctrl-drag copies file contents");
            Equal("Nested file", File.ReadAllText(Path.Combine(c.RightPath, "folder1", "inside.txt")), "Ctrl-drag copies folder contents");
            await FileDragProbe.NativeAsync([Path.Combine(c.LeftPath, "file2.log")], c.RightPath, 0, 2);
            Check(!File.Exists(Path.Combine(c.LeftPath, "file2.log")) && File.Exists(Path.Combine(c.RightPath, "file2.log")), "An ordinary drag on the same volume moves the file");
            await FileDragProbe.NativeAsync([Path.Combine(c.LeftPath, "folder2")], c.RightPath, 8 | 4, 4);
            Check(Directory.EnumerateFiles(c.RightPath, "*.lnk").Any(), "Ctrl+Shift-drag creates a Windows shortcut");
            Check(Directory.Exists(Path.Combine(c.LeftPath, "folder2")), "Creating a shortcut preserves the source folder");
        })
    ];
}
