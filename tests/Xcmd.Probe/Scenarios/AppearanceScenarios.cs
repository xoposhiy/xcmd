// @tests FindViewerText, ReaderThemes, RestoreWorkspace, ReportControl, KeepFeedback, SharedControls
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Input;
using Xcmd.Desktop;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.AppearanceProbe;

namespace Xcmd.Probe.Scenarios;

public static class AppearanceScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("appearance-main-title-and-footer", "UC20; shared title bar and compact footer feedback controls", async c =>
        {
            CheckMainFrame(c.Window);
            await CheckMenusAsync(c, c.Window, c.Window.TitleBar.Children.OfType<Menu>().Single());
            await c.ScreenshotAsync("main-combined-header");
            c.Window.Width = 840; c.Window.Height = 400;
            c.Window.SetUpdateMessage("A long update message keeps the window controls and file panels available.");
            await c.SettleAsync(); CheckMainFrame(c.Window);
            await c.ScreenshotAsync("main-combined-header-narrow");
            await c.ClickAsync(WorkspaceProbe.Button(c.Window, "MainMaximize"));
            Equal(WindowState.Maximized, c.Window.WindowState, "The main header maximizes the window");
            await c.ClickAsync(WorkspaceProbe.Button(c.Window, "MainMaximize"));
            Equal(WindowState.Normal, c.Window.WindowState, "The main header restores the window");
            await c.ClickAsync(WorkspaceProbe.Button(c.Window, "StartFeedback"));
            Check(c.Window.Feedback.IsPicking, "The footer feedback control starts selection");
            await c.KeyAsync(Key.Escape);
            await c.ClickAsync(WorkspaceProbe.Button(c.Window, "ShowNotes"));
            Check(c.Window.Feedback.ActiveNotes != null, "The footer notes control opens the saved list");
            await c.KeyAsync(Key.Escape, target: c.Window.Feedback.ActiveNotes);
        }),
        new("appearance-reader-mdview-themes-and-persistence", "UC09; MD View palettes, reading state and saved preference", async c =>
        {
            var store = new SessionStore(Path.Combine(c.FixturePath, "appearance-session.json"));
            await store.SaveAsync(c.Window.CaptureWorkspace());
            using var lifecycle = new WorkspaceLifecycle(c.Window, store, null);
            await lifecycle.InitializeAsync([]);
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.F3);
            var first = c.Window.Viewer!;
            ViewerProbe.CheckTitleBar(first);
            await CheckMenusAsync(c, first, first.Reader.Commands);
            var names = ThemeMenu(first).Items.OfType<MenuItem>().Select(item => item.Header?.ToString()).ToArray();
            Check(names.SequenceEqual(new[] { "Cream", "GitHub", "Solarized Light", "Forest Light", "Dark", "Solarized Dark", "Dracula", "Nord", "Forest Dark" }), "The theme menu matches MD View");
            await ViewerProbe.StartSearchAsync(c, first, "a");
            await ChooseThemeAsync(c, first, "light");
            CheckReaderColors(first, "#fff2cf", "#000000");
            Equal("a", first.Reader.TextContent.SelectedText, "Changing the palette preserves the current match");
            Check(first.Reader.IsSearchVisible && first.Reader.Search.Text == "a", "Changing the palette preserves the query");
            await c.ScreenshotAsync("reader-cream", first);
            await ChooseThemeAsync(c, first, "dracula");
            CheckReaderColors(first, "#282a36", "#f8f8f2");
            await c.ScreenshotAsync("reader-dracula", first);
            Equal("dracula", (await store.ReadAsync())!.ViewerThemeId, "A theme choice is saved immediately");
            await c.ClickAsync(c.Row("file2.log")); await c.KeyAsync(Key.F3);
            var second = c.Window.Viewers.Last();
            Equal("dracula", second.Reader.ThemeId, "New viewers use the chosen palette");
            await ChooseThemeAsync(c, second, "forest-light");
            Equal("forest-light", first.Reader.ThemeId, "Open viewers share the reader preference");
            Equal("abc", first.Reader.TextContent.Text, "Theme changes preserve existing contents");
            var saved = (await store.ReadAsync())!;
            foreach (var viewer in c.Window.Viewers.ToArray()) viewer.Close();
            lifecycle.Dispose();
            await CheckRestoredThemeAsync(c, saved);
            var legacy = JsonNode.Parse(await File.ReadAllTextAsync(store.Path))!.AsObject();
            legacy.Remove("ViewerThemeId"); await File.WriteAllTextAsync(store.Path, legacy.ToJsonString());
            Equal("dark", (await store.ReadAsync())!.ViewerThemeId, "Sessions without a theme keep a valid default");
        })
    ];
}
