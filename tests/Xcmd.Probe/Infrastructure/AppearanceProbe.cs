// @implements HeadlessVerification
// @tests ReaderThemes, RestoreWorkspace, ReportControl, SharedControls, HeadlessVerification
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop;

namespace Xcmd.Probe.Infrastructure;

public static class AppearanceProbe
{
    public static void CheckMainFrame(CommanderWindow window)
    {
        ProbeContext.Equal(WindowDecorations.BorderOnly, window.WindowDecorations, "The main window uses the combined title row");
        ProbeContext.Equal(WindowDecorationsElementRole.TitleBar, WindowDecorationProperties.GetElementRole(window.TitleBar), "The combined row supports native dragging");
        CheckHeaderInput(window, window.TitleBar);
        foreach (var name in new[] { "StartFeedback", "ShowNotes" })
        {
            var button = WorkspaceProbe.Button(window, name);
            var position = button.TranslatePoint(default, window)!.Value;
            ProbeContext.Check(position.Y >= window.ClientSize.Height - 32 && position.X >= window.ClientSize.Width - 230, "Feedback controls are at the bottom right");
        }
        var close = WorkspaceProbe.Button(window, "MainCloseWindow").TranslatePoint(default, window)!.Value;
        ProbeContext.Check(close.Y < 32 && close.X >= window.ClientSize.Width - 40, "The close control is at the top-right edge");
    }

    public static void CheckHeaderInput(Window window, Grid header)
    {
        var menu = header.Children.OfType<Menu>().Single();
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            var point = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), window)!.Value;
            ProbeContext.Equal<WindowDecorationsElementRole?>(WindowDecorationsElementRole.User, ChromeRoleAt(window, point), "Header menus receive client input instead of starting a window drag");
        }
        var title = header.Children.OfType<TextBlock>().Single(item => Grid.GetColumn(item) == 1);
        var titlePoint = title.TranslatePoint(new Point(title.Bounds.Width / 2, title.Bounds.Height / 2), window)!.Value;
        ProbeContext.Equal<WindowDecorationsElementRole?>(WindowDecorationsElementRole.TitleBar, ChromeRoleAt(window, titlePoint), "The title remains a native window drag region");
    }

    private static WindowDecorationsElementRole? ChromeRoleAt(Window window, Point point)
    {
        // Headless pointer events skip the chrome hit-test used by the Windows backend.
        var hitTest = typeof(IInputRoot).GetMethod("HitTestChromeElement", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var inputRoot = typeof(TopLevel).GetProperty("InputRoot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(window);
        return (WindowDecorationsElementRole?)hitTest.Invoke(inputRoot, [point]);
    }

    public static async Task CheckMenusAsync(ProbeContext c, Window window, Menu menu)
    {
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            await c.ClickAsync(item, target: window);
            ProbeContext.Check(item.IsSubMenuOpen, "A header menu opens with one click");
            await c.KeyAsync(Key.Escape, target: window);
            ProbeContext.Check(!item.IsSubMenuOpen, "Escape closes the header menu");
        }
    }

    public static MenuItem ThemeMenu(FileViewerWindow viewer) => ((MenuItem)viewer.Reader.Commands.Items[1]!).Items.OfType<MenuItem>().Single(item => item.Name == "ViewerThemes");

    public static async Task ChooseThemeAsync(ProbeContext c, FileViewerWindow viewer, string id)
    {
        await c.ClickAsync((MenuItem)viewer.Reader.Commands.Items[1]!, target: viewer);
        var themes = ThemeMenu(viewer);
        await c.ClickAsync(themes, target: viewer);
        await c.ClickAsync(themes.Items.OfType<MenuItem>().Single(item => item.Name == "viewer.theme." + id), target: viewer);
    }

    public static void CheckReaderColors(FileViewerWindow viewer, string background, string foreground)
    {
        ProbeContext.Equal(Color.Parse(background), (viewer.Reader.TextContent.Background as ISolidColorBrush)?.Color, "Reader background matches the MD View palette");
        ProbeContext.Equal(Color.Parse(foreground), (viewer.Reader.TextContent.Foreground as ISolidColorBrush)?.Color, "Reader text matches the MD View palette");
        ProbeContext.Equal(Color.Parse(background), (viewer.Reader.TitleBar.Background as ISolidColorBrush)?.Color, "Title and content use the same palette");
        var border = viewer.Reader.TextContent.GetVisualDescendants().OfType<Border>().Single(item => item.Name == "PART_BorderElement");
        ProbeContext.Equal(Color.Parse(background), (border.Background as ISolidColorBrush)?.Color, "The actual focused text surface uses the palette");
    }

    public static async Task CheckRestoredThemeAsync(ProbeContext c, WorkspaceState state)
    {
        var window = new CommanderWindow(c.FileSystem, c.Launcher);
        try
        {
            window.Show(); await window.RestoreWorkspaceAsync(state);
            ProbeContext.Equal(state.ViewerThemeId, window.ViewerThemeId, "A new application window restores the saved reader theme");
            var input = new InputDriver(window);
            input.KeyDown(Key.F3, RawInputModifiers.None, PhysicalKey.F3, null); input.KeyUp(Key.F3, RawInputModifiers.None, PhysicalKey.F3, null);
            await WorkspaceProbe.WaitAsync(() => !window.IsBusy);
            ProbeContext.Equal(state.ViewerThemeId, window.Viewer?.Reader.ThemeId, "The restored preference reaches a new viewer");
            var handle = window.Viewer!.TryGetPlatformHandle();
            ProbeContext.Check(handle?.Handle == 0 && handle.HandleDescriptor == "STUB", "Restored viewers remain headless");
        }
        finally { window.Close(); }
    }
}
